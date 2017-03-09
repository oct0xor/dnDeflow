/*
    dnDeflow - Control flow deobfuscation using Z3 and ILAst
    Copyright (C) 2016 oct0xor@gmail.com

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.ComponentModel.Composition;
using dnSpy.Contracts.Extension;
using dnSpy.Contracts.Output;
using dnSpy.Contracts.Text;

namespace DeFlow
{
    class OutputLog
    {
        public static readonly Guid LOG_GUID = new Guid("FFAB16BE-189C-4328-B6B9-5414F711A1CE");
        public static IOutputTextPane Instance
        {
            get
            {
                if (_instance == null)
                    throw new InvalidOperationException("Logger hasn't been initialized yet");
                return _instance;
            }

            set
            {
                if (_instance != null)
                    throw new InvalidOperationException("Can't initialize the logger twice");
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                _instance = value;
            }
        }

        static IOutputTextPane _instance;

        [ExportAutoLoaded(Order = double.MaxValue)]
        sealed class InitializeLogger : IAutoLoaded
        {
            [ImportingConstructor]
            InitializeLogger(IOutputService outputService)
            {
                Instance = outputService.Create(LOG_GUID, "DeFlow Output");
                Instance.WriteLine("DeFlow initialized");
            }
        }
    }
}
