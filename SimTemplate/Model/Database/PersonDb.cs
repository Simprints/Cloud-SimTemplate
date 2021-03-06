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
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimTemplate.Model.Database
{
    [Table(Name = "Person")]
    public class PersonDb
    {
        [Column(IsPrimaryKey = true, IsDbGenerated = true)]
        public Int64 id { get; set; }

        [Column]
        public string Pid { get; set; }

        private EntitySet<CaptureDb> _Captures;
        [Association(Storage = "_Captures", OtherKey = "PersonId")]
        public EntitySet<CaptureDb> Captures
        {
            get { return this._Captures; }
            set { this._Captures.Assign(value); }
        }
    }
}
