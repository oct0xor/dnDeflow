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
using System.Windows.Threading;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnSpy.AsmEditor.UndoRedo;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.Tabs.DocViewer;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Menus;

namespace DeFlow
{
    class Proxy
    {
        class ProxyMenuItem
        {
            [ExportMenuItem(OwnerGuid = AppMenuConstants.APP_MENU_EXTENSION, Header = "Remove Proxy Methods", Group = AppMenuConstants.GROUP_EXTENSION_MENU3, Order = 0)]
            sealed class ProxyMethodCommand : AppMenuHandler
            {
                readonly Lazy<IUndoCommandService> undoCommandService;
                readonly Lazy<IMethodAnnotations> methodAnnotations;
                readonly IAppService appService;

                [ImportingConstructor]
                ProxyMethodCommand(Lazy<IUndoCommandService> undoCommandService, Lazy<IMethodAnnotations> methodAnnotations, IAppService appService)
                        : base(appService.DocumentTreeView)
                {
                    this.undoCommandService = undoCommandService;
                    this.methodAnnotations = methodAnnotations;
                    this.appService = appService;
                }

                public override bool IsEnabled(DeFlowContext context)
                {
                    return ProxyMethod.CanExecute(context.Nodes);
                }

                public override void Execute(DeFlowContext context)
                {
                    ProxyMethod.Execute(methodAnnotations, undoCommandService, appService, context.Nodes);
                }
            }
        }

        sealed class ProxyMethod : IUndoCommand
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

                    undoCommandService.Value.Add(new ProxyMethod(methodAnnotations.Value, methodNode, token));

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
            readonly List<MethodDef> methods;
            readonly List<MethodBody> origMethodBodys;
            List<bool> isBodyModified;
            CancellationToken token;

            ProxyMethod(IMethodAnnotations methodAnnotations, IMethodNode methodNode, CancellationToken token)
            {
                this.methodAnnotations = methodAnnotations;
                this.methodNode = methodNode;
                this.token = token;

                this.methods = new List<MethodDef>();
                this.origMethodBodys = new List<MethodBody>();
                this.isBodyModified = new List<bool>();
            }

            public string Description => "Remove Proxy Methods";

            public void Execute()
            {
                if (token.IsCancellationRequested)
                {
                    // it means its called from Redo command
                    token = default(CancellationToken);
                }

                try
                {
                    methods.Add(methodNode.MethodDef);
                    origMethodBodys.Add(methodNode.MethodDef.MethodBody);
                    isBodyModified.Add(methodAnnotations.IsBodyModified(methodNode.MethodDef));
                    methodAnnotations.SetBodyModified(methodNode.MethodDef, true);

                    var method = methodNode.MethodDef;

                    if (method.Body.Instructions.Any(x => x.OpCode.Code == Code.Call && x.Operand is MethodDef))
                    {
                        //At first lets restore all methods called from this method

                        for (int i = 0; i < method.Body.Instructions.Count(); i++)
                        {
                            if (method.Body.Instructions[i].OpCode.Code == Code.Call && method.Body.Instructions[i].Operand is MethodDef && (method.Body.Instructions[i].Operand as MethodDef).Body != null)
                            {
                                methods.Add(method.Body.Instructions[i].Operand as MethodDef);
                                origMethodBodys.Add((method.Body.Instructions[i].Operand as MethodDef).MethodBody);
                                isBodyModified.Add(methodAnnotations.IsBodyModified(method.Body.Instructions[i].Operand as MethodDef));
                                methodAnnotations.SetBodyModified(method.Body.Instructions[i].Operand as MethodDef, true);

                                MethodDeobfuscator.Deobfuscate(method.Body.Instructions[i].Operand as MethodDef, token);
                            }
                        }

                        var tempMethod = DotNetUtils.Clone(method);
                        var blocks = new Blocks(tempMethod);

                        List<Block> allBlocks = new List<Block>();
                        blocks.MethodBlocks.GetAllBlocks(allBlocks);

                        foreach (var block in allBlocks)
                        {
                            for (int i = 0; i < block.Instructions.Count; i++)
                            {
                                var instruction = block.Instructions[i];

                                if (instruction.OpCode.Code == Code.Call && instruction.Operand is MethodDef && (instruction.Operand as MethodDef).Body != null)
                                {
                                    // Remove empty method

                                    if ((instruction.Operand as MethodDef).Body.Instructions.All(x => x.OpCode.Code == Code.Nop || x.OpCode.Code == Code.Ret))
                                    {
                                        block.Instructions.RemoveAt(i);

                                        //TODO: remove all inner empty methods, but better to have addititonal command for it tho
                                    }

                                    // Inline proxy method

                                    if ((instruction.Operand as MethodDef).Body.Instructions.All(x => IsOkOpcode(x.OpCode.Code)))
                                    {
                                        var inlined = GetInstruction((instruction.Operand as MethodDef).Body.Instructions);

                                        if (inlined != null)
                                        {
                                            block.Instructions.RemoveAt(i);
                                            block.Instructions.Insert(i, new Instr(new Instruction(inlined.OpCode, inlined.Operand)));
                                        }
                                    }
                                }
                            }
                        }

                        IList<Instruction> allInstructions;
                        IList<ExceptionHandler> allExceptionHandlers;
                        blocks.GetCode(out allInstructions, out allExceptionHandlers);
                        DotNetUtils.RestoreBody(tempMethod, (IEnumerable<Instruction>)allInstructions, (IEnumerable<ExceptionHandler>)allExceptionHandlers);

                        MethodDeobfuscator.RestoreMethod(method, tempMethod);
                    }
                }
                catch (OperationCanceledException)
                {

                }
            }

            public void Undo()
            {
                for (int i = 0; i < this.isBodyModified.Count; i++)
                {
                    methods[i].MethodBody = origMethodBodys[i];
                    methodAnnotations.SetBodyModified(methods[i], isBodyModified[i]);
                    methods[i].Body.UpdateInstructionOffsets();
                }

                this.methods.Clear();
                this.origMethodBodys.Clear();
                this.isBodyModified.Clear();
            }

            public IEnumerable<object> ModifiedObjects
            {
                get { yield return methodNode.GetModuleNode(); }
            }
        }

        private static bool IsOkOpcode(Code code)
        {
            switch (code)
            {
                case Code.Ldarg:
                case Code.Ldarg_S:
                case Code.Ldarg_0:
                case Code.Ldarg_1:
                case Code.Ldarg_2:
                case Code.Ldarg_3:
                case Code.Ret:

                case Code.Newobj:
                case Code.Call:
                case Code.Callvirt:

                case Code.Ldc_I4:
                case Code.Ldc_I4_0:
                case Code.Ldc_I4_1:
                case Code.Ldc_I4_2:
                case Code.Ldc_I4_3:
                case Code.Ldc_I4_4:
                case Code.Ldc_I4_5:
                case Code.Ldc_I4_6:
                case Code.Ldc_I4_7:
                case Code.Ldc_I4_8:
                case Code.Ldc_I4_M1:
                case Code.Ldc_I4_S:
                case Code.Ldc_I8:
                case Code.Ldc_R4:
                case Code.Ldc_R8:

                case Code.Ldstr:

                    return true;

                default:
                    return false;
            }
        }

        private static Instruction GetInstruction(dnlib.Threading.Collections.IList<Instruction> Instructions)
        {
            Code[] opcodes = new Code[] {
                Code.Newobj,
                Code.Call,
                Code.Callvirt,

                Code.Ldc_I4,
                Code.Ldc_I4_0,
                Code.Ldc_I4_1,
                Code.Ldc_I4_2,
                Code.Ldc_I4_3,
                Code.Ldc_I4_4,
                Code.Ldc_I4_5,
                Code.Ldc_I4_6,
                Code.Ldc_I4_7,
                Code.Ldc_I4_8,
                Code.Ldc_I4_M1,
                Code.Ldc_I4_S,
                Code.Ldc_I8,
                Code.Ldc_R4,
                Code.Ldc_R8,

                Code.Ldstr
            };

            foreach (var opcode in opcodes)
            {
                if (Instructions.Where(x => x.OpCode.Code == opcode).Count() == 1)
                {
                    var others = opcodes.Where(x => x != opcode);

                    if (Instructions.All(x => others.All(y => x.OpCode.Code != y)))
                    {
                        return Instructions.First(x => x.OpCode.Code == opcode);
                    }
                }
            }

            return null;
        }
    }
}
