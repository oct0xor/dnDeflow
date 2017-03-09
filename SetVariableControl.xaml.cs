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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using dnSpy.Contracts.Controls;
using dnSpy.Contracts.MVVM;

namespace DeFlow
{
    sealed partial class SetVariableControl : WindowBase
    {
        private object fields;

        public SetVariableControl()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.fields = this.DataContext;
            this.DataContext = SetVariable.Variables;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new SetVariableControlNew();
            win.DataContext = this.fields;
            win.Owner = Window.GetWindow(this);
            win.ShowDialog();
        }

        private void DelButton_Click(object sender, RoutedEventArgs e)
        {
            if (Vars.SelectedIndex != -1)
                SetVariable.Variables.Items.RemoveAt(Vars.SelectedIndex);
        }
    }
}
