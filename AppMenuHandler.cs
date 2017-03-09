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
using System.Linq;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Menus;

namespace DeFlow
{
    sealed class DeFlowContext
    {
        public IDocumentTreeNodeData[] Nodes { get; }

        public DeFlowContext(IDocumentTreeNodeData[] nodes)
        {
            this.Nodes = nodes;
        }
    }

    abstract class AppMenuHandler : MenuItemBase<DeFlowContext>
    {
        protected sealed override object CachedContextKey => ContextKey;
        static readonly object ContextKey = new object();

        readonly IDocumentTreeView documentTreeView;

        protected AppMenuHandler(IDocumentTreeView documentTreeView)
        {
            this.documentTreeView = documentTreeView;
        }

        protected sealed override DeFlowContext CreateContext(IMenuItemContext context)
        {
            if (context.CreatorObject.Guid != new Guid(AppMenuConstants.APP_MENU_EXTENSION))
                return null;
            return this.CreateContext();
        }

        private DeFlowContext CreateContext() => new DeFlowContext(this.documentTreeView.TreeView.TopLevelSelection.OfType<IDocumentTreeNodeData>().ToArray());
    }
}
