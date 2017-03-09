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
using System.ComponentModel.Composition;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Menus;
using dnSpy.Contracts.MVVM;

namespace DeFlow
{
    public class DeFlowSettings
    {
        public class MySettings : ViewModelBase
        {
            protected virtual void OnModified()
            {
            }

            bool flag = true;
            internal bool Remove
            {
                get { return flag; }
                set
                {
                    if (flag == value)
                        return;

                    flag = value;
                    OnPropertyChanged("BoolEnable");
                    OnPropertyChanged("BoolDisable");
                    DeadInstructions.DeadInstrsList.Clear();
                }
            }

            public bool BoolEnable
            {
                get { return Remove == true; }
                set { Remove = value ? true : Remove; }
            }

            public bool BoolDisable
            {
                get { return Remove == false; }
                set { Remove = value ? false : Remove; }
            }

            bool flag2 = true;
            internal bool Repartition
            {
                get { return flag2; }
                set
                {
                    if (flag2 == value)
                        return;

                    flag2 = value;
                    OnPropertyChanged("BoolEnable2");
                    OnPropertyChanged("BoolDisable2");
                }
            }

            public bool BoolEnable2
            {
                get { return Repartition == true; }
                set { Repartition = value ? true : Repartition; }
            }

            public bool BoolDisable2
            {
                get { return Repartition == false; }
                set { Repartition = value ? false : Repartition; }
            }

            bool flag3 = true;
            internal bool Nops
            {
                get { return flag3; }
                set
                {
                    if (flag3 == value)
                        return;

                    flag3 = value;
                    OnPropertyChanged("BoolEnable3");
                    OnPropertyChanged("BoolDisable3");
                }
            }

            public bool BoolEnable3
            {
                get { return Nops == true; }
                set { Nops = value ? true : Nops; }
            }

            public bool BoolDisable3
            {
                get { return Nops == false; }
                set { Nops = value ? false : Nops; }
            }

            bool flag4 = false;

            internal bool Logs
            {
                get { return flag4; }
                set
                {
                    if (flag4 == value)
                        return;

                    flag4 = value;
                    OnPropertyChanged("BoolEnable4");
                    OnPropertyChanged("BoolDisable4");
                }
            }

            public bool BoolEnable4
            {
                get { return Logs == true; }
                set { Logs = value ? true : Logs; }
            }

            public bool BoolDisable4
            {
                get { return Logs == false; }
                set { Logs = value ? false : Logs; }
            }
        }

        public static MySettings Settings = new MySettings();

        [ExportMenuItem(OwnerGuid = AppMenuConstants.APP_MENU_EXTENSION, Header = "Options", Icon = DsImagesAttribute.Settings, Group = AppMenuConstants.GROUP_EXTENSION_MENU4, Order = 0)]
        sealed class SettingsCommand : AppMenuHandler
        {
            readonly IAppService appService;

            [ImportingConstructor]
            SettingsCommand(IAppService appService)
                    : base(appService.DocumentTreeView)
            {
                this.appService = appService;
            }

            public override bool IsEnabled(DeFlowContext context)
            {
                return true;
            }

            public override void Execute(DeFlowContext context)
            {
                var win = new SettingsControl();
                win.DataContext = Settings;
                win.Owner = this.appService.MainWindow;
                win.ShowDialog();
            }
        }
    }
}
