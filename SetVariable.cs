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
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Menus;
using dnSpy.Contracts.MVVM;

namespace DeFlow
{
    public class SetVariable
    {
        public class Var
        {
            public string Token { get; set; }

            public string Name { get; set; }

            public uint Value { get; set; }
        }

        public class ViewModel : ViewModelBase
        {
            public ObservableCollection<Var> Items { get; set; }

            public ViewModel()
            {
                this.Items = new ObservableCollection<Var>();
            }
        }

        public static ViewModel Variables = new ViewModel();

        //[ExportMenuItem(OwnerGuid = AppMenuConstants.APP_MENU_EXTENSION, Header = "Set Variables", Icon = DsImagesAttribute.MarkupTag, Group = AppMenuConstants.GROUP_EXTENSION_MENU2, Order = 0)]
        sealed class SetVariableCommand : AppMenuHandler
        {
            readonly IAppService appService;

            [ImportingConstructor]
            SetVariableCommand(IAppService appService)
                    : base(appService.DocumentTreeView)
            {
                this.appService = appService;
            }

            public override bool IsEnabled(DeFlowContext context)
            {
                return context.Nodes.Length == 1 && context.Nodes[0] is IMethodNode && (context.Nodes[0] as IMethodNode).MethodDef.HasBody;
            }

            public override void Execute(DeFlowContext context)
            {
                var methodNode = (IMethodNode)context.Nodes[0];

                var module = context.Nodes[0].GetModule();
                Debug.Assert(module != null);
                if (module == null)
                    throw new InvalidOperationException();

                var method = methodNode.MethodDef;

                List<FieldDef> fields = new List<FieldDef>();

                foreach (var instr in method.Body.Instructions)
                {
                    if (instr.Operand is FieldDef)
                    {
                        fields.Add(instr.Operand as FieldDef);
                    }
                }

                fields = fields.Distinct().ToList();

                var win = new SetVariableControl();
                win.DataContext = fields;
                win.Owner = this.appService.MainWindow;
                win.ShowDialog();
            }
        }
    }
}
