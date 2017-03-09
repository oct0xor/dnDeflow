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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using dnlib.DotNet;
using dnSpy.Contracts.Controls;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.MVVM;
using ICSharpCode.Decompiler.Disassembler;

namespace DeFlow
{
    sealed partial class SetVariableControlNew : WindowBase
    {
        public SetVariableControlNew()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var field in (this.DataContext as List<FieldDef>))
            {
                Vars.Items.Add(this.GetField(field));
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            Regex regex1 = new Regex("^\\d+$");
            Regex regex2 = new Regex("^-\\d+$");
            Regex regex3 = new Regex("^[A-Fa-f0-9]{1,8}$");
            Regex regex4 = new Regex("^0x[A-Fa-f0-9]{1,8}$");

            if (SetVariable.Variables.Items.Where(x => x.Name == Vars.Text).Any())
                return;

            if (regex1.IsMatch(Value.Text))
            {
                uint val = 0;
                if (uint.TryParse(Value.Text, out val))
                {
                    var varName = Vars.Text;
                    var token = (this.DataContext as List<FieldDef>).Find(x => this.GetField(x) == varName).MDToken.ToUInt32();

                    SetVariable.Variables.Items.Add(new SetVariable.Var() { Token = string.Format("0x{0:X8}", token), Name = varName, Value = val });
                }
            }
            else if (regex2.IsMatch(Value.Text))
            {
                int val = 0;
                if (int.TryParse(Value.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val))
                {
                    var varName = Vars.Text;
                    var token = (this.DataContext as List<FieldDef>).Find(x => this.GetField(x) == varName).MDToken.ToUInt32();

                    SetVariable.Variables.Items.Add(new SetVariable.Var() { Token = string.Format("0x{0:X8}", token), Name = varName, Value = (uint)val });
                }
            }
            else if (regex3.IsMatch(Value.Text))
            {
                uint val = 0;
                if (uint.TryParse(Value.Text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out val))
                {
                    var varName = Vars.Text;
                    var token = (this.DataContext as List<FieldDef>).Find(x => this.GetField(x) == varName).MDToken.ToUInt32();

                    SetVariable.Variables.Items.Add(new SetVariable.Var() { Token = string.Format("0x{0:X8}", token), Name = varName, Value = val });
                }
            }
            else if (regex4.IsMatch(Value.Text))
            {
                uint val = 0;
                if (uint.TryParse(Value.Text.Remove(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out val))
                {
                    var varName = Vars.Text;
                    var token = (this.DataContext as List<FieldDef>).Find(x => this.GetField(x) == varName).MDToken.ToUInt32();

                    SetVariable.Variables.Items.Add(new SetVariable.Var() { Token = string.Format("0x{0:X8}", token), Name = varName, Value = val });
                }
            }
        }

        private string GetField(IField field)
        {
            IDecompilerOutput output;
            var stringBuilder = new StringBuilder();
            var writer = new StringWriter(stringBuilder);
            output = new TextWriterDecompilerOutput(writer);

            DisassemblerHelpers.WriteFieldTo(field, output);

            return output.ToString();
        }
    }
}
