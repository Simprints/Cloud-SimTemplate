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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SimTemplate.Utilities;
using SimTemplate.DataTypes;
using SimTemplate.DataTypes.Enums;

namespace SimTemplate.Helpers.Test
{
    [TestClass]
    public class IsoTemplateHelperTest
    {
        private const string TEMPLATE_1_HEX = "464D5200203230000000003C0000012C019000C500C5010000105B054087000B660080B5003B6700407100176C00407600346D0080A0004BE2000000";
        private static readonly IEnumerable<MinutiaRecord> TEMPLATE_1_MINUTAE = new List<MinutiaRecord>()
        {
            { new MinutiaRecord(new Point(135.918, 11.429), 143.746, MinutiaType.Termination) },
            { new MinutiaRecord(new Point(181.633, 59.592), 145.008, MinutiaType.Bifurication) },
            { new MinutiaRecord(new Point(113.061, 23.265), 152.103, MinutiaType.Termination) },
            { new MinutiaRecord(new Point(118.776, 52.653), 154.654, MinutiaType.Termination) },
            { new MinutiaRecord(new Point(160, 75.918), 319.086, MinutiaType.Bifurication) },
        };

        private const string TEMPLATE_2_HEX = "464D5200203230000000003C0000012C019000C500C5010000105B0580DE01C857004039017A7400807B0035F90040E7FFFBC00080507652E2000000";
        private static readonly IEnumerable<MinutiaRecord> TEMPLATE_2_MINUTAE = new List<MinutiaRecord>()
        {
            { new MinutiaRecord(new Point(222.1, 456.7), 123.41234, MinutiaType.Bifurication) },
            { new MinutiaRecord(new Point(57.2, 378.999), 164.5, MinutiaType.Termination) },
            { new MinutiaRecord(new Point(123.667, 53.1234), 350.2346, MinutiaType.Bifurication) },
            { new MinutiaRecord(new Point(231.223, 65531.12345), 270.4235, MinutiaType.Termination) },
            { new MinutiaRecord(new Point(80, 30290.1235), 319.086, MinutiaType.Bifurication) },
        };

        [TestMethod]
        public void TestConvertToIsoTemplate1()
        {
            TestToIsoTemplate(TEMPLATE_1_MINUTAE, TEMPLATE_1_HEX);
        }

        [TestMethod]
        public void TestConvertToIsoTemplate2()
        {
            TestToIsoTemplate(TEMPLATE_2_MINUTAE, TEMPLATE_2_HEX);
        }

        [TestMethod]
        public void TestConvertNullToIsoTemplate()
        {
            SimTemplateException m_Exception = null;
            try
            {
                byte[] template = IsoTemplateHelper.ToIsoTemplate(null);
            }
            catch (SimTemplateException ex)
            {
                m_Exception = ex;
            }
            Assert.IsNotNull(m_Exception);
        }

        #region Helper Methods

        private void TestToIsoTemplate(IEnumerable<MinutiaRecord> minutae, string isoTemplateHex)
        {
            // Get the IsoTemplate
            byte[] template = IsoTemplateHelper.ToIsoTemplate(minutae);
            // Convert the IsoTemplate back to a list of minutia (loss of data in casting)
            IEnumerable<MinutiaRecord> convert_minutae = IsoTemplateHelper.ToMinutae(template);

            // Convert it to Hex for comparison
            string templateHex = BitConverter.ToString(template);
            templateHex = templateHex.Replace("-", String.Empty);

            // Assertions
            CollectionAssert.AreEqual(IsoTemplateHelper.ToByteArray(isoTemplateHex), template);
            Assert.AreEqual(minutae.Count(), convert_minutae.Count());
            for (int i = 0; i < convert_minutae.Count(); i++)
            {
                MinutiaRecord real_minutia = minutae.ElementAt(i);
                MinutiaRecord converted_minutia = convert_minutae.ElementAt(i);
                Assert.AreEqual((int)real_minutia.Position.X, converted_minutia.Position.X);
                Assert.AreEqual((int)real_minutia.Position.Y, converted_minutia.Position.Y);
                // y(x,a) = ax - floor(ax)
                // max(y(x,a)) = 1, min(y(x,a)) = 0
                // e(x,a) = x - x_hat =  1/a * floor(ax) = 1/a * y(x,a)
                // Thus max(e(x,a)) = 1/a, min(e(x,a)) = 0
                Assert.IsTrue(real_minutia.Angle - converted_minutia.Angle < 1.0 / (256 / 360));
            }
        }

        #endregion
    }
}
