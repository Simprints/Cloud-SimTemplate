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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SimTemplate.Utilities;
using SimTemplate.ViewModels;
using SimTemplate.DataTypes;
using SimTemplate.DataTypes.Enums;
using SimTemplate.ViewModels.Interfaces;

namespace SimTemplate.ViewModels
{
    public partial class TemplatingViewModel
    {
        public class WaitDirection : Initialised
        {
            private const string SET_ANGLE_PROMPT = "Please set minutia angle";

            private MinutiaRecord m_Record;

            public WaitDirection(TemplatingViewModel outer) : base(outer)
            { }

            #region Overriden Public Methods

            public override void OnEnteringState()
            {
                base.OnEnteringState();

                Outer.OnUserActionRequired(new UserActionRequiredEventArgs(SET_ANGLE_PROMPT));

                // Get the minutia that was placed in the previous step
                IntegrityCheck.AreNotEqual(0, Outer.Minutae.Count());
                m_Record = Outer.Minutae.Last();
                IntegrityCheck.IsNotNull(m_Record.Position);
            }

            public override void PositionUpdate(Point position)
            {
                // Update the direction whenever the mouse moves.
                SetDirection(position);
            }

            public override void PositionInput(Point position)
            {
                // The user has just finalised the direction of the minutia.
                SetDirection(position);
                TransitionTo(typeof(WaitLocation));
            }

            public override void RemoveMinutia(int index)
            {
                //Do nothing.
            }

            public override void SetMinutiaType(MinutiaType type)
            {
                // Update minutia type as user has changed it.
                m_Record.Type = Outer.InputMinutiaType;
            }

            public override void EscapeAction()
            {
                // Cancel adding the current minutia.
                Outer.Minutae.Remove(m_Record);
                TransitionTo(typeof(WaitLocation));
            }

            #endregion

            #region Private Methods

            private void SetDirection(Point p)
            {
                // Get the relevant record
                Vector direction = p - m_Record.Position;

                // Calculate the angle (in degrees)
                double angle = MathsHelper.RadianToDegree(Math.Atan2(direction.Y, direction.X));
                
                // Save the new direction
                m_Record.Angle = angle;
            }

            public override void StartMove(int index)
            {
                // The user may click the minutia when setting direction but this shouldn't start a
                // move!
                // Ignore.
            }

            #endregion
        }
    }
}
