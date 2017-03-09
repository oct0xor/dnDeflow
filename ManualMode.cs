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
using System.Threading;
using dnSpy.AsmEditor.UndoRedo;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Menus;
using dnSpy.Contracts.MVVM;
using dnSpy.Contracts.Themes;

namespace DeFlow
{
    class ManualMode
    {
        public static ITheme Theme;

        [ExportMenuItem(OwnerGuid = AppMenuConstants.APP_MENU_EXTENSION, Header = "Manual Mode", Icon = DsImagesAttribute.MarkupTag, Group = AppMenuConstants.GROUP_EXTENSION_MENU2, Order = 0)]
        sealed class ManualModeCommand : AppMenuHandler
        {
            readonly IAppService appService;
            readonly IThemeService themeService;
            readonly Lazy<IMethodAnnotations> methodAnnotations;
            readonly Lazy<IUndoCommandService> undoCommandService;

            [ImportingConstructor]
            ManualModeCommand(IAppService appService, IThemeService themeService, Lazy<IMethodAnnotations> methodAnnotations, Lazy<IUndoCommandService> undoCommandService)
                    : base(appService.DocumentTreeView)
            {
                this.appService = appService;
                this.themeService = themeService;
                this.methodAnnotations = methodAnnotations;
                this.undoCommandService = undoCommandService;
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

                Theme = themeService.Theme;

                var win = new ManualModeControl();
                win.DataContext = new object[3] { methodNode, this.methodAnnotations, this.undoCommandService };
                win.Owner = this.appService.MainWindow;
                win.ShowDialog();
            }
        }
    }
}
