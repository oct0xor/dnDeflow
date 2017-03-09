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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnSpy.AsmEditor.UndoRedo;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.Tabs;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Images;
using dnSpy.Contracts.Menus;
using dnSpy.Contracts.Text;

namespace DeFlow
{
    class Solvers
    {
        [ExportMenu(OwnerGuid = MenuConstants.APP_MENU_GUID, Guid = AppMenuConstants.APP_MENU_EXTENSION, Order = MenuConstants.ORDER_APP_MENU_DEBUG - 0.1, Header = "_DeFlow")]
        sealed class DebugMenu : IMenu
        {
        }

        class SolverMenuItem1
        {
            [ExportMenuItem(OwnerGuid = AppMenuConstants.APP_MENU_EXTENSION, Header = "Solve Method", Icon = DsImagesAttribute.Binary, Group = AppMenuConstants.GROUP_EXTENSION_MENU1, Order = 0)]
            sealed class SolveMethodCommand : AppMenuHandler
            {
                readonly Lazy<IUndoCommandService> undoCommandService;
                readonly Lazy<IMethodAnnotations> methodAnnotations;
                readonly IAppService appService;

                [ImportingConstructor]
                SolveMethodCommand(Lazy<IUndoCommandService> undoCommandService, Lazy<IMethodAnnotations> methodAnnotations, IAppService appService)
                        : base(appService.DocumentTreeView)
                {
                    this.undoCommandService = undoCommandService;
                    this.methodAnnotations = methodAnnotations;
                    this.appService = appService;
                }

                public override bool IsEnabled(DeFlowContext context)
                {
                    return SolveMethod.CanExecute(context.Nodes);
                }

                public override void Execute(DeFlowContext context)
                {
                    SolveMethod.Execute(methodAnnotations, undoCommandService, appService, context.Nodes);
                }
            }
        }

        class SolverMenuItem2
        {
            [ExportMenuItem(OwnerGuid = AppMenuConstants.APP_MENU_EXTENSION, Header = "Solve Methods (In Type)", Icon = DsImagesAttribute.Binary, Group = AppMenuConstants.GROUP_EXTENSION_MENU1, Order = 1)]
            sealed class SolveMethodsCommand : AppMenuHandler
            {
                readonly Lazy<IUndoCommandService> undoCommandService;
                readonly Lazy<IMethodAnnotations> methodAnnotations;
                readonly IAppService appService;

                [ImportingConstructor]
                SolveMethodsCommand(Lazy<IUndoCommandService> undoCommandService, Lazy<IMethodAnnotations> methodAnnotations, IAppService appService)
                        : base(appService.DocumentTreeView)
                {
                    this.undoCommandService = undoCommandService;
                    this.methodAnnotations = methodAnnotations;
                    this.appService = appService;
                }

                public override bool IsEnabled(DeFlowContext context)
                {
                    return SolveMethods.CanExecute(context.Nodes);
                }

                public override void Execute(DeFlowContext context)
                {
                    SolveMethods.Execute(methodAnnotations, undoCommandService, appService, context.Nodes);
                }
            }
        }

        class SolverMenuItem3
        {
            [ExportMenuItem(OwnerGuid = AppMenuConstants.APP_MENU_EXTENSION, Header = "Solve All Methods (In Module)", Icon = DsImagesAttribute.BinaryFile, Group = AppMenuConstants.GROUP_EXTENSION_MENU1, Order = 2)]
            sealed class SolveMethodsCommand : AppMenuHandler
            {
                readonly Lazy<IUndoCommandService> undoCommandService;
                readonly Lazy<IMethodAnnotations> methodAnnotations;
                readonly IAppService appService;

                [ImportingConstructor]
                SolveMethodsCommand(Lazy<IUndoCommandService> undoCommandService, Lazy<IMethodAnnotations> methodAnnotations, IAppService appService)
                        : base(appService.DocumentTreeView)
                {
                    this.undoCommandService = undoCommandService;
                    this.methodAnnotations = methodAnnotations;
                    this.appService = appService;
                }

                public override bool IsEnabled(DeFlowContext context)
                {
                    return SolveAllMethods.CanExecute(context.Nodes);
                }

                public override void Execute(DeFlowContext context)
                {
                    SolveAllMethods.Execute(methodAnnotations, undoCommandService, appService, context.Nodes);
                }
            }
        }

        sealed class SolveMethod : IUndoCommand
        {
            internal static bool CanExecute(IDocumentTreeNodeData[] nodes) => nodes.Length == 1 && nodes[0] is IMethodNode && (nodes[0] as IMethodNode).MethodDef.HasBody;

            internal static void Execute(Lazy<IMethodAnnotations> methodAnnotations, Lazy<IUndoCommandService> undoCommandService, IAppService appService, IDocumentTreeNodeData[] nodes, uint[] offsets = null)
            {
                if (!CanExecute(nodes))
                    return;

                var methodNode = (IMethodNode)nodes[0];

                var module = nodes[0].GetModule();
                Debug.Assert(module != null);
                if (module == null)
                    throw new InvalidOperationException();

                var documentTab = appService.DocumentTabService.ActiveTab;
                var documentViewer = appService.DocumentTabService.ActiveTab?.UIContext as IDocumentViewer;

                CancellationToken token = default(CancellationToken);
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

                documentTab.AsyncExec(cs => {
                    token = cs.Token;
                    documentViewer.ShowCancelButton("Deobfuscation...", () => cs.Cancel());
                }, () => {

                    undoCommandService.Value.Add(new SolveMethod(methodAnnotations.Value, methodNode, DeadInstructions.DeadInstrsList, token));

                    dispatcher.Invoke(() =>
                    {
                        appService.DocumentTabService.RefreshModifiedDocument(nodes[0].GetDocumentNode().Document);
                    });

                }, result => {
                    documentViewer.HideCancelButton();
                });
            }

            readonly IMethodAnnotations methodAnnotations;
            readonly IMethodNode methodNode;
            readonly MethodBody origMethodBody;
            readonly DeadInstr[] deadInstructions;
            CancellationToken token;
            bool isBodyModified;

            SolveMethod(IMethodAnnotations methodAnnotations, IMethodNode methodNode, List<DeadInstr> deadInstructions, CancellationToken token)
            {
                this.methodAnnotations = methodAnnotations;
                this.methodNode = methodNode;
                this.origMethodBody = methodNode.MethodDef.MethodBody;
                this.deadInstructions = new DeadInstr[deadInstructions.Count];
                deadInstructions.CopyTo(this.deadInstructions);
                this.token = token;
            }

            public string Description => "Solve Method";

            public void Execute()
            {
                if (token.IsCancellationRequested)
                {
                    // it means its called from Redo command
                    token = default(CancellationToken);
                }

                try
                {
                    if (DeFlowSettings.Settings.Logs)
                        OutputLog.Instance.WriteLine("Processing method: " + string.Format("0x{0:X8}", methodNode.MethodDef.MDToken.Raw));

                    isBodyModified = methodAnnotations.IsBodyModified(methodNode.MethodDef);
                    methodAnnotations.SetBodyModified(methodNode.MethodDef, true);

                    MethodDeobfuscator.Deobfuscate(methodNode.MethodDef, token);
                }
                catch (OperationCanceledException)
                {

                }
            }

            public void Undo()
            {
                methodNode.MethodDef.MethodBody = origMethodBody;
                methodAnnotations.SetBodyModified(methodNode.MethodDef, isBodyModified);
                methodNode.MethodDef.Body.UpdateInstructionOffsets();
                DeadInstructions.DeadInstrsList.Clear();
                DeadInstructions.DeadInstrsList.AddRange(deadInstructions);
            }

            public IEnumerable<object> ModifiedObjects
            {
                get { yield return methodNode; }
            }
        }

        sealed class SolveMethods : IUndoCommand
        {
            internal static bool CanExecute(IDocumentTreeNodeData[] nodes) => nodes.Length == 1 && nodes[0] is ITypeNode;

            internal static void Execute(Lazy<IMethodAnnotations> methodAnnotations, Lazy<IUndoCommandService> undoCommandService, IAppService appService, IDocumentTreeNodeData[] nodes, uint[] offsets = null)
            {
                if (!CanExecute(nodes))
                    return;

                var typeNode = (ITypeNode)nodes[0];

                var module = nodes[0].GetModule();
                Debug.Assert(module != null);
                if (module == null)
                    throw new InvalidOperationException();

                var documentTab = appService.DocumentTabService.ActiveTab;
                var documentViewer = appService.DocumentTabService.ActiveTab?.UIContext as IDocumentViewer;

                CancellationToken token = default(CancellationToken);
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

                documentTab.AsyncExec(cs => {
                    token = cs.Token;
                    documentViewer.ShowCancelButton("Deobfuscation...", () => cs.Cancel());
                }, () => {

                    undoCommandService.Value.Add(new SolveMethods(methodAnnotations.Value, typeNode, DeadInstructions.DeadInstrsList, token));

                    dispatcher.Invoke(() =>
                    {
                        appService.DocumentTabService.RefreshModifiedDocument(nodes[0].GetDocumentNode().Document);
                    });

                }, result => {
                    documentViewer.HideCancelButton();
                });
            }

            readonly IMethodAnnotations methodAnnotations;
            readonly ITypeNode typeNode;
            readonly List<MethodDef> methods;
            readonly List<MethodBody> origMethodBodys;
            readonly DeadInstr[] deadInstructions;
            CancellationToken token;
            List<bool> isBodyModified;

            SolveMethods(IMethodAnnotations methodAnnotations, ITypeNode typeNode, List<DeadInstr> deadInstructions, CancellationToken token)
            {
                this.methodAnnotations = methodAnnotations;
                this.typeNode = typeNode;

                this.methods = new List<MethodDef>();
                AddMethods(this.methods, typeNode.TypeDef);

                this.origMethodBodys = new List<MethodBody>();
                foreach (var method in this.methods)
                    this.origMethodBodys.Add(method.MethodBody);

                this.deadInstructions = new DeadInstr[deadInstructions.Count];
                deadInstructions.CopyTo(this.deadInstructions);

                this.isBodyModified = new List<bool>();

                this.token = token;
            }

            public string Description => "Solve Methods";

            public void Execute()
            {
                if (token.IsCancellationRequested)
                {
                    // it means its called from Redo command
                    token = default(CancellationToken);
                }

                if (DeFlowSettings.Settings.Logs)
                    OutputLog.Instance.WriteLine("Methods in type: " + this.methods.Count.ToString());

                foreach (var method in this.methods)
                {
                    try
                    {
                        if (DeFlowSettings.Settings.Logs)
                            OutputLog.Instance.WriteLine("Processing method: " + string.Format("0x{0:X8}", method.MDToken.Raw));

                        isBodyModified.Add(methodAnnotations.IsBodyModified(method));
                        methodAnnotations.SetBodyModified(method, true);

                        MethodDeobfuscator.Deobfuscate(method, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            public void Undo()
            {
                for (int i = 0; i < this.isBodyModified.Count; i++)
                {
                    methods[i].MethodBody = origMethodBodys[i];
                    methodAnnotations.SetBodyModified(methods[i], isBodyModified[i]);
                    methods[i].Body.UpdateInstructionOffsets();
                    DeadInstructions.DeadInstrsList.Clear();
                    DeadInstructions.DeadInstrsList.AddRange(deadInstructions);
                }

                this.isBodyModified.Clear();
            }

            public IEnumerable<object> ModifiedObjects
            {
                get { yield return typeNode; }
            }
        }

        sealed class SolveAllMethods : IUndoCommand
        {
            internal static bool CanExecute(IDocumentTreeNodeData[] nodes) => nodes.Length == 1 && nodes[0].GetModuleNode() != null;

            internal static void Execute(Lazy<IMethodAnnotations> methodAnnotations, Lazy<IUndoCommandService> undoCommandService, IAppService appService, IDocumentTreeNodeData[] nodes, uint[] offsets = null)
            {
                if (!CanExecute(nodes))
                    return;

                var moduleNode = nodes[0].GetModuleNode();

                var module = nodes[0].GetModule();
                Debug.Assert(module != null);
                if (module == null)
                    throw new InvalidOperationException();

                var documentTab = appService.DocumentTabService.ActiveTab;
                var documentViewer = appService.DocumentTabService.ActiveTab?.UIContext as IDocumentViewer;

                CancellationToken token = default(CancellationToken);
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

                documentTab.AsyncExec(cs => {
                    token = cs.Token;
                    documentViewer.ShowCancelButton("Deobfuscation...", () => cs.Cancel());
                }, () => {

                    undoCommandService.Value.Add(new SolveAllMethods(methodAnnotations.Value, moduleNode, DeadInstructions.DeadInstrsList, token));

                    dispatcher.Invoke(() =>
                    {
                        appService.DocumentTabService.RefreshModifiedDocument(nodes[0].GetDocumentNode().Document);
                    });

                }, result => {
                    documentViewer.HideCancelButton();
                });
            }

            readonly IMethodAnnotations methodAnnotations;
            readonly IModuleDocumentNode moduleNode;
            readonly List<MethodDef> methods;
            readonly List<MethodBody> origMethodBodys;
            readonly DeadInstr[] deadInstructions;
            CancellationToken token;
            List<bool> isBodyModified;

            SolveAllMethods(IMethodAnnotations methodAnnotations, IModuleDocumentNode moduleNode, List<DeadInstr> deadInstructions, CancellationToken token)
            {
                this.methodAnnotations = methodAnnotations;
                this.moduleNode = moduleNode;

                this.methods = new List<MethodDef>();

                var module = moduleNode.GetModule();
                if (module.HasTypes)
                {
                    foreach (TypeDef type in (IEnumerable<TypeDef>)module.Types)
                        AddMethods(methods, type);
                }

                this.origMethodBodys = new List<MethodBody>();
                foreach (var method in this.methods)
                    this.origMethodBodys.Add(method.MethodBody);

                this.deadInstructions = new DeadInstr[deadInstructions.Count];
                deadInstructions.CopyTo(this.deadInstructions);

                this.isBodyModified = new List<bool>();

                this.token = token;
            }

            public string Description => "Solve All Methods";

            public void Execute()
            {
                if (token.IsCancellationRequested)
                {
                    // it means its called from Redo command
                    token = default(CancellationToken);
                }

                if (DeFlowSettings.Settings.Logs)
                    OutputLog.Instance.WriteLine("Methods in module: " + this.methods.Count.ToString());

                foreach (var method in this.methods)
                {
                    try
                    {
                        if (DeFlowSettings.Settings.Logs)
                            OutputLog.Instance.WriteLine("Processing method: " + string.Format("0x{0:X8}", method.MDToken.Raw));

                        isBodyModified.Add(methodAnnotations.IsBodyModified(method));
                        methodAnnotations.SetBodyModified(method, true);

                        MethodDeobfuscator.Deobfuscate(method, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            public void Undo()
            {
                for (int i = 0; i < this.isBodyModified.Count; i++)
                {
                    methods[i].MethodBody = origMethodBodys[i];
                    methodAnnotations.SetBodyModified(methods[i], isBodyModified[i]);
                    methods[i].Body.UpdateInstructionOffsets();
                    DeadInstructions.DeadInstrsList.Clear();
                    DeadInstructions.DeadInstrsList.AddRange(deadInstructions);
                }

                this.isBodyModified.Clear();
            }

            public IEnumerable<object> ModifiedObjects
            {
                get { yield return moduleNode; }
            }
        }

        public static void AddMethods(List<MethodDef> methods, TypeDef type)
        {
            if (type.HasMethods)
            {
                foreach (MethodDef methodDef in (IEnumerable<MethodDef>)type.Methods)
                {
                    if (methodDef.HasBody)
                        methods.Add(methodDef);
                }
            }

            if (!type.HasNestedTypes)
                return;

            foreach (TypeDef type1 in (IEnumerable<TypeDef>)type.NestedTypes)
                AddMethods(methods, type1);
        }
    }
}
