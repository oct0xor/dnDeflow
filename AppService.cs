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

using System.ComponentModel.Composition;
using System.Windows;
using dnSpy.Contracts.App;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents.Tabs;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Extension;

namespace DeFlow
{
    interface IAppService
    {
        Window MainWindow { get; }
        IDocumentTabService DocumentTabService { get; }
        IDocumentTreeView DocumentTreeView { get; }
        IDecompilerService DecompilerManager { get; }
    }

    [Export(typeof(IAppService))]
    sealed class AppService : IAppService, IAutoLoaded
    {
        IAppWindow AppWindow { get; }
        public Window MainWindow => this.AppWindow.MainWindow;
        public IDocumentTabService DocumentTabService { get; }
        public IDocumentTreeView DocumentTreeView { get; }
        public IDecompilerService DecompilerManager { get; }

        [ImportingConstructor]
        AppService(IAppWindow appWindow, IDocumentTabService documentTabService, IDocumentTreeView documentTreeView, IDecompilerService decompilerManager)
        {
            this.AppWindow = appWindow;
            this.DocumentTabService = documentTabService;
            this.DocumentTreeView = documentTreeView;
            this.DecompilerManager = decompilerManager;
        }
    }
}
