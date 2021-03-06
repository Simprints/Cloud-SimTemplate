using SimTemplate.DataTypes.Enums;
using SimTemplate.Utilities;
using SimTemplate.Model.DataControllers;
using SimTemplate.Model.DataControllers.EventArguments;
using SimTemplate.StateMachine;
using SimTemplate.ViewModels.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using SimTemplate.ViewModels.Interfaces;

namespace SimTemplate.ViewModels
{
    // TODO: Could make generic method that takes:
    // where T1 : ViewModel, ITemplatingViewModel
    // where T2 : ViewModel, ISettingsViewModel
    public partial class MainWindowViewModel : ViewModel, IMainWindowViewModel
    {
        // State machine members
        private readonly StateManager<MainWindowState> m_StateMgr;
        private readonly object m_StateLock;

        // Dependencies
        private readonly IDataController m_DataController;
        private readonly ISettingsManager m_SettingsManager;
        private readonly IWindowService m_WindowService;
        private readonly IDispatcherHelper m_DispatcherHelper;

        // Child ViewModels
        private readonly ITemplatingViewModel m_TemplatingViewModel;
        private readonly ISettingsViewModel m_SettingsViewModel;

        // ViewModel-driven members
        private Activity m_CurrentActivity;
        private SimTemplateException m_Exception;
        private string m_PromptText;

        // View-driven members
        private ScannerType m_FilteredScannerType;

        // Commands
        private ICommand m_LoadFileCommand;
        private ICommand m_SaveTemplateCommand;
        private ICommand m_TerminationButtonPressCommand;
        private ICommand m_BifuricationButtonPressCommand;
        private ICommand m_EscapePressCommand;
        private ICommand m_ToggleSettingsCommand;
        private ICommand m_InitialiseCommand;

        private event EventHandler<ActivityChangedEventArgs> m_ActivityChanged;

        #region Constructor
        // TODO: Swap TemplatingViewModel for a TemplatingViewModelFactory
        // TODO: Swap SettingsViewModel for a SettingsViewModelFactory
        public MainWindowViewModel(
            IDataController dataController,
            ITemplatingViewModel templatingViewModel,
            ISettingsViewModel settingsViewModel,
            ISettingsManager settingsManager,
            IWindowService windowService,
            IDispatcherHelper dispatcherHelper)
        {
            IntegrityCheck.IsNotNull(dataController);
            IntegrityCheck.IsNotNull(templatingViewModel);
            IntegrityCheck.IsNotNull(settingsViewModel);
            IntegrityCheck.IsNotNull(settingsManager);
            IntegrityCheck.IsNotNull(windowService);
            IntegrityCheck.IsNotNull(dispatcherHelper);

            // Save arguments
            m_DataController = dataController;
            m_TemplatingViewModel = templatingViewModel;
            m_SettingsViewModel = settingsViewModel;
            m_SettingsManager = settingsManager;
            m_WindowService = windowService;
            m_DispatcherHelper = dispatcherHelper;

            // Initialise the state machine
            m_StateLock = new object();
            m_StateMgr = new StateManager<MainWindowState>(this, typeof(Uninitialised));

            // Configure commands/event handlers
            InitialiseCommands();
            m_DataController.InitialisationComplete += DataController_InitialisationComplete;
            m_DataController.GetCaptureComplete += DataController_GetCaptureComplete;
            m_DataController.SaveTemplateComplete += DataController_SaveTemplateComplete;
            m_TemplatingViewModel.UserActionRequired += TemplatingViewModel_UserActionRequired;
        }

        #endregion

        /// <summary>
        /// Gets the exception that may have occurred.
        /// </summary>
        /// <value>
        /// The exception. null if there is no exception.
        /// </value>
        public SimTemplateException Exception { get { return m_Exception; } }

        /// <summary>
        /// Gets the dispatcher helper to update UI elements.
        /// </summary>
        /// <value>
        /// The dispatcher helper.
        /// </value>
        public IDispatcherHelper DispatcherHelper { get { return m_DispatcherHelper; } }

        #region IMainWindowViewModel

        void IMainWindowViewModel.BeginInitialise()
        {
            BeginInitialise();
        }

        event EventHandler<ActivityChangedEventArgs> IMainWindowViewModel.ActivityChanged
        {
            add { m_ActivityChanged += value; }
            remove { m_ActivityChanged -= value; }
        }

        #endregion

        #region Directly Bound Properties

        public string PromptText
        {
            get { return m_PromptText; }
            set
            {
                if (value != m_PromptText)
                {
                    m_PromptText = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public ITemplatingViewModel TemplatingViewModel
        {
            get { return m_TemplatingViewModel; }
        }

        /// <summary>
        /// Gets a list of scanner type optitons.
        /// </summary>
        /// <value>
        /// The scanner types.
        /// </value>
        public IEnumerable<ScannerType> ScannerTypes
        {
            get
            {
                return Enum.GetValues(typeof(ScannerType))
                    .Cast<ScannerType>();
            }
        }

        /// <summary>
        /// Gets or sets the type of scanner type to filter GET requests for.
        /// </summary>
        /// <value>
        /// The type of scanner to filter.
        /// </value>
        public ScannerType FilteredScannerType
        {
            get { return m_FilteredScannerType; }
            set
            {
                if (value != m_FilteredScannerType)
                {
                    lock (m_StateLock)
                    {
                        m_FilteredScannerType = value;
                        m_StateMgr.State.SetScannerType(m_FilteredScannerType);
                    }
                    NotifyPropertyChanged();
                }
            }
        }

        // TODO: Replace this with single enum indicating state, and use converter
        /// <summary>
        /// Gets or sets a value indicating whether the ViewModel is currently templating.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is templating; otherwise, <c>false</c>.
        /// </value>
        public bool IsTemplating
        {
            get { return (m_StateMgr.State.GetType() == typeof(Templating)); }
        }

        public Activity CurrentActivity
        {
            get { return m_CurrentActivity; }
            set
            {
                if (m_CurrentActivity != value)
                {
                    m_CurrentActivity = value;
                    NotifyPropertyChanged();
                }
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Gets the 'load file' command for binding to the 'openFile' button.
        /// </summary>
        public ICommand LoadFileCommand { get { return m_LoadFileCommand; } }

        /// <summary>
        /// Gets the 'save template' command for binding to the 'Save' button.
        /// </summary>
        public ICommand SaveTemplateCommand { get { return m_SaveTemplateCommand; } }

        /// <summary>
        /// Gets the termination button press command for binding to the 't' key.
        /// </summary>
        public ICommand TerminationButtonPressCommand { get { return m_TerminationButtonPressCommand; } }

        /// <summary>
        /// Gets the bifurication button press command for binding to the 'b' key.
        /// </summary>
        public ICommand BifuricationButtonPressCommand { get { return m_BifuricationButtonPressCommand; } }

        /// <summary>
        /// Gets the escape press command for binding to the Esc key.
        /// </summary>
        public ICommand EscapePressCommand { get { return m_EscapePressCommand; } }

        public ICommand ToggleSettingsCommand { get { return m_ToggleSettingsCommand; } }

        public ICommand ReinitialiseCommand { get { return m_InitialiseCommand; } }

        #endregion

        #region Private Methods

        private void InitialiseCommands()
        {
            m_LoadFileCommand = new RelayCommand(
                x => LoadFile());
            m_SaveTemplateCommand = new RelayCommand(
                x => SaveTemplate(),
                x => m_TemplatingViewModel.IsSaveTemplatePermitted);
            m_TerminationButtonPressCommand = new RelayCommand(
                x => m_TemplatingViewModel.InputMinutiaType = MinutiaType.Termination,
                x => IsTemplating);
            m_BifuricationButtonPressCommand = new RelayCommand(
                x => m_TemplatingViewModel.InputMinutiaType = MinutiaType.Bifurication,
                x => IsTemplating);
            m_EscapePressCommand = new RelayCommand(
                x => EscapeAction());
            m_ToggleSettingsCommand = new RelayCommand(x => OpenSettings());
            m_InitialiseCommand = new RelayCommand(x => BeginInitialise());
        }

        #region Command Callbacks

        private void BeginInitialise()
        {
            Log.Debug("BeginInitialise() called.");
            lock (m_StateLock)
            {

                m_StateMgr.State.BeginInitialise();
            }
        }

        private void LoadFile()
        {
            Log.Debug("LoadFile() called.");
            lock (m_StateLock)
            {
                m_StateMgr.State.LoadFile();
            }
        }

        private void SaveTemplate()
        {
            Log.Debug("SaveTemplate() called.");
            lock (m_StateLock)
            {
                m_StateMgr.State.SaveTemplate();
            }
        }
        private void EscapeAction()
        {
            Log.Debug("EscapeAction() called.");
            lock (m_StateLock)
            {
                m_StateMgr.State.EscapeAction();
            }
        }

        private void OpenSettings()
        {
            Log.Debug("OpenSettings() called.");

            // Refresh the view model with latest settings
            // TODO: Create a new SettingsViewModel (using a factory) rather than refresh
            m_SettingsViewModel.Refresh();
            // Present opportunity to change settings
            m_WindowService.ShowDialog(m_SettingsViewModel);

            if (m_SettingsViewModel.Result == ViewModelStatus.Complete)
            {
                Log.Debug("Settings were updated, re-initialising...");
                // Settings were updated, we must re-initialise the DataController
                lock (m_StateLock)
                {
                    m_StateMgr.TransitionTo(typeof(Initialising));
                }
            }
            else
            {
                Log.Debug("Settings were not updated");
            }
        }

        #endregion

        #endregion

        #region Event Handlers

        private void DataController_InitialisationComplete(object sender, InitialisationCompleteEventArgs e)
        {
            Log.DebugFormat(
                "DataController_InitialisationComplete(Result={0}) called.",
                e.Result);
            lock (m_StateLock)
            {
                m_StateMgr.State.DataController_InitialisationComplete(e);
            }
        }

        private void DataController_GetCaptureComplete(object sender, GetCaptureCompleteEventArgs e)
        {
            Log.DebugFormat(
                "DataController_GetCaptureComplete(Capture={0}) called.",
                e.Capture);
            lock (m_StateLock)
            {
                m_StateMgr.State.DataController_GetCaptureComplete(e);
            }
        }


        private void DataController_SaveTemplateComplete(object sender, SaveTemplateEventArgs e)
        {
            Log.DebugFormat(
                "DataController_SaveTemplateComplete(Result={0}) called.",
                e.Result);
            lock (m_StateLock)
            {
                m_StateMgr.State.DataController_SaveTemplateComplete(e);
            }
        }

        private void TemplatingViewModel_UserActionRequired(object sender, UserActionRequiredEventArgs e)
        {
            PromptText = e.PromptText;
        }

        #endregion

        #region Event Helpers

        private void OnActivityChanged(ActivityChangedEventArgs e)
        {
            EventHandler<ActivityChangedEventArgs> temp = m_ActivityChanged;
            if (m_ActivityChanged != null)
            {
                m_ActivityChanged.Invoke(this, e);
            }
        }

        #endregion
    }
}
