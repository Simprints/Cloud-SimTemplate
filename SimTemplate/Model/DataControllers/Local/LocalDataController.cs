﻿// Copyright 2016 Sam Briggs
//
// This file is part of SimTemplate.
//
// SimTemplate is free software: you can redistribute it and/or modify it under the
// terms of the GNU General Public License as published by the Free Software 
// Foundation, either version 3 of the License, or (at your option) any later
// version.
//
// SimTemplate is distributed in the hope that it will be useful, but WITHOUT ANY 
// WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR 
// A PARTICULAR PURPOSE. See the GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License along with
// SimTemplate. If not, see http://www.gnu.org/licenses/.
//
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using SimTemplate.Utilities;
using SimTemplate.Model.DataControllers.EventArguments;
using SimTemplate.Model.Database;
using SimTemplate.DataTypes.Enums;
using SimTemplate.DataTypes;

namespace SimTemplate.Model.DataControllers.Local
{
    public class LocalDataController : DataController
    {
        #region Constants

        private const string SQLITE_DATABASE = @"C:\SimPrints\Data\mainDb_yesFMR_noPNG.sqlite";
        private const string IMAGES_FILE_PATH = @"C:\SimPrints\Images\Data_Zambia";

        private const int MAX_OPEN_FILE_ATTEMPTS = 1000;
        private const string CONNECTION_STRING = @"Data Source={0};Version=3;";
        private const string CAPTURE_QUERY_STRING = @"SELECT * FROM Capture WHERE Capture.SimAfisTemplate IS {0} ORDER BY RANDOM() LIMIT 1;";
        private const string CAPTURE_GIVEN_SCANNER_QUERY_STRING = @"SELECT * FROM Capture WHERE Capture.SimAfisTemplate IS {0} AND ScannerName = '{1}' ORDER BY RANDOM() LIMIT 1;";

        #endregion

        private SimPrintsDb m_Database;
        private InitialisationResult m_State;
        private IEnumerable<string> m_ImageFiles;

        #region Constructor

        public LocalDataController() : base()
        {
            m_State = InitialisationResult.Uninitialised;
        }

        #endregion

        #region Private Methods

        protected override void StartInitialiseTask(DataControllerConfig config, Guid guid, CancellationToken token)
        {
            // Define and run the task, passing in the token.
            Task initialiseTask = Task.Run(() =>
            {
                Log.Debug("Initialise task running.");
                // Connect to SQlite.
                SQLiteConnection dbConnection = new SQLiteConnection(
                String.Format(CONNECTION_STRING, SQLITE_DATABASE));

                // Set the LINQ data context to the database connection.
                m_Database = new SimPrintsDb(dbConnection);

                // Obtain image files on local machine, to be matched with database entries.
                m_ImageFiles = GetImageFiles(IMAGES_FILE_PATH);
                if (m_ImageFiles != null &&
                    m_ImageFiles.Count() > 0)
                {
                    m_State = InitialisationResult.Initialised;
                }
                else
                {
                    Log.Error("Failed to get image files.");
                    m_State = InitialisationResult.Error;
                }

                OnInitialisationComplete(
                    new InitialisationCompleteEventArgs(m_State, guid, DataRequestResult.Success));
            }, token);

            // Raise the GetCaptureComplete event in the case where the Task faults.
            initialiseTask.ContinueWith((Task t) =>
            {
                if (t.IsFaulted)
                {
                    Log.Error("Failed initialise controller: " + t.Exception.Message, t.Exception);
                    OnInitialisationComplete(new InitialisationCompleteEventArgs(
                        InitialisationResult.Uninitialised, guid, DataRequestResult.TaskFailed));
                }
            });
        }

        protected override void StartCaptureTask(ScannerType scannerType, Guid guid, CancellationToken token)
        {
            // Define and run the task, passing in the token.
            Task getCaptureTask = Task.Run(() =>
            {
                Log.Debug("Get capture task running.");
                // Get a capture
                CaptureInfo capture = GetCapture(scannerType, token);
                // Raise GetCaptureComplete event.
                OnGetCaptureComplete(new GetCaptureCompleteEventArgs(capture, guid, DataRequestResult.Success));
            }, token);

            // Raise the GetCaptureComplete event in the case where the Task faults.
            getCaptureTask.ContinueWith((Task t) =>
            {
                if (t.IsFaulted)
                {
                    Log.Error("Failed to save template: " + t.Exception.Message, t.Exception);
                    OnGetCaptureComplete(new GetCaptureCompleteEventArgs(null, guid, DataRequestResult.TaskFailed));
                }
            });
        }

        protected override void StartSaveTask(long dbId, byte[] template, Guid guid, CancellationToken token)
        {
            Task saveTask = Task.Run(() =>
            {
                Log.Debug("Save task running.");

                CaptureDb capture = (from c in m_Database.Captures
                                     where c.Id == dbId
                                     select c).FirstOrDefault();

                if (capture != null)
                {
                    // Update the template to that supplied
                    capture.GoldTemplate = template;
                    m_Database.SubmitChanges();
                }
                else
                {
                    Log.WarnFormat("Failed to find capture wtih DbId={0}. Not saving template", dbId);
                }
            }, token);

            // Raise the SaveTemplateComplete event in the case where the Task faults.
            saveTask.ContinueWith((Task t) =>
            {
                if (t.IsFaulted)
                {
                    Log.Error("Failed to save template: " + t.Exception.Message, t.Exception);
                    OnSaveTemplateComplete(new SaveTemplateEventArgs(guid, DataRequestResult.TaskFailed));
                }
            });
        }

        private CaptureInfo GetCapture(ScannerType scannerType, CancellationToken token)
        {
            Log.DebugFormat("GetCapture(scannerType={0}, token={1}) called",
                scannerType, token);
            CaptureInfo captureInfo = null;
            DataRequestResult result = DataRequestResult.None;
            bool isRunning = true;
            int attempts = 0;
            while (isRunning)
            {
                // Check if cancellation requested
                token.ThrowIfCancellationRequested();

                // First query the database to get an image file name.
                CaptureDb captureCandidate = GetCaptureFromDatabase(scannerType);

                if (captureCandidate != null)
                {
                    // Try to find an image file using the file name.
                    byte[] imageData;
                    bool isFound = TryGetImageFromName(captureCandidate.HumanId, out imageData);
                    if (isFound)
                    {
                        // Matching file found.
                        Log.DebugFormat("Matching file found for capture={0}", captureCandidate.HumanId);
                        isRunning = false;
                        captureInfo = new CaptureInfo(
                            captureCandidate.Id,
                            imageData,
                            captureCandidate.GoldTemplate);
                        result = DataRequestResult.Success;
                    }
                    else
                    {
                        // Give up if the number of attemps exceeds limit.
                        attempts++;
                        if (attempts > MAX_OPEN_FILE_ATTEMPTS)
                        {
                            Log.WarnFormat("Exceeded maximum number of file searches (attempts={0})",
                                attempts);
                            isRunning = false;
                            result = DataRequestResult.Failed;
                        }
                    }
                }
                else
                {
                    // Queries are not returning any more candidates, give up immediately.
                    Log.Warn("No candidate filename obtained from the database");
                    result = DataRequestResult.Failed;
                    break;
                }
            }
            IntegrityCheck.AreNotEqual(DataRequestResult.None, result);
            return captureInfo;
        }

        private IEnumerable<string> GetImageFiles(string directory)
        {
            ConcurrentBag<string> threadSafeImages = null;
            if (Directory.Exists(directory))
            {
                // The provided image path exists, so fetch images in that directory.
                IEnumerable<string> imageFiles = Directory.GetFiles(
                    directory,
                    "*.png",
                    SearchOption.AllDirectories);
                // Save the image files in a thread-safe list.
                threadSafeImages = new ConcurrentBag<string>(imageFiles);
            }
            else
            {
                Log.ErrorFormat(
                    "Supplied directory for image files does not exist ({0})",
                    directory);
            }
            return threadSafeImages;
        }

        private CaptureDb GetCaptureFromDatabase(ScannerType scannerType)
        {
            CaptureDb capture;
            string withTemplateString = "NULL";
            string query;
            if (scannerType == ScannerType.None)
            {
                query = String.Format(CAPTURE_QUERY_STRING, withTemplateString);
            }
            else
            {
                query = String.Format(CAPTURE_GIVEN_SCANNER_QUERY_STRING, withTemplateString, scannerType);
            }
            capture = m_Database.ExecuteQuery<CaptureDb>(query).FirstOrDefault();
            return capture;
        }

        private bool TryGetImageFromName(string name, out byte[] imageData)
        {
            // Try to find an image file using the filename obtained from the database.
            string filepath;
            bool isFound = TryGetPathFromName(name, out filepath);

            bool isSuccessful = false;
            imageData = null;
            if (isFound)
            {
                if (File.Exists(filepath))
                {
                    // The file exists.
                    Log.DebugFormat("An image file was found for image: {0}.", filepath);
                    try
                    {
                        imageData = File.ReadAllBytes(filepath);
                        isSuccessful = true;
                    }
                    catch (NotSupportedException ex)
                    {
                        Log.WarnFormat("Failed to read image file: {0}", ex);
                    }
                }
                else
                {
                    Log.WarnFormat(
                        "File {0} found in candidate files on local machine but file no longer exists.",
                        name);
                }
            }
            else
            {
                Log.WarnFormat("Found no image file containing {0}", name);
            }
            return isSuccessful;
        }

        private bool TryGetPathFromName(string name, out string filepath)
        {
            filepath = m_ImageFiles.FirstOrDefault(x => x.Contains(name));
            return !String.IsNullOrEmpty(filepath);
        }

        #endregion
    }
}
