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
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using dnSpy.Contracts.Extension;

namespace DeFlow
{
    [ExportExtension]
    sealed class TheExtension : IExtension
    {
        public IEnumerable<string> MergedResourceDictionaries
        {
            get
            {
                yield break;
            }
        }
        
        public ExtensionInfo ExtensionInfo => new ExtensionInfo
        {
            ShortDescription = "Control flow deobfuscation using Z3 and ILAst",
        };
        
        public void OnEvent(ExtensionEvent @event, object obj)
        {
            if (@event == ExtensionEvent.Loaded)
            {
                var currentDomain = AppDomain.CurrentDomain;
                var location = Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(location);
                
                if (assemblyDir != null)
                {
                    currentDomain.AssemblyResolve += (sender, arg) =>
                    {
                        if (arg.Name.StartsWith("Microsoft.Z3,", StringComparison.OrdinalIgnoreCase))
                        {
                            string fileName = Path.Combine(assemblyDir, string.Format("{0}\\Microsoft.Z3.dll", (Environment.Is64BitProcess) ? "x64" : "x86"));
                            return Assembly.LoadFile(fileName);
                        }
                    
                        return null;
                    };
                }
            }
        }
    }
}
