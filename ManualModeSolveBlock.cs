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
using System.Threading;
using de4dot.blocks;
using dnlib.DotNet.Emit;
using dnSpy.AsmEditor.UndoRedo;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.TreeView;
using Microsoft.Z3;

namespace DeFlow
{
    sealed class SolveBlock : IUndoCommand
    {
        readonly IMethodAnnotations methodAnnotations;
        readonly IMethodNode methodNode;
        readonly MethodBody origMethodBody;
        bool isBodyModified;
        Context ctx;
        BoolExpr expr;
        Block block;

        public SolveBlock(IMethodAnnotations methodAnnotations, IMethodNode methodNode, Context ctx, BoolExpr expr, Block block)
        {
            this.methodAnnotations = methodAnnotations;
            this.methodNode = methodNode;
            this.origMethodBody = methodNode.MethodDef.MethodBody;
            this.ctx = ctx;
            this.expr = expr;
            this.block = block;
        }

        public string Description => "Solve Block";

        public void Execute()
        {
            isBodyModified = methodAnnotations.IsBodyModified(methodNode.MethodDef);
            methodAnnotations.SetBodyModified(methodNode.MethodDef, true);

            DeadInstructions.DeadInstrsList.Clear();

            CancellationToken token = default(CancellationToken);
            MethodDeobfuscator.DeobfuscateAssisted(methodNode.MethodDef, token, ctx, expr, block);
        }

        public void Undo()
        {
            methodNode.MethodDef.MethodBody = origMethodBody;
            methodAnnotations.SetBodyModified(methodNode.MethodDef, isBodyModified);
            methodNode.MethodDef.Body.UpdateInstructionOffsets();

            DeadInstructions.DeadInstrsList.Clear();
        }

        public IEnumerable<object> ModifiedObjects
        {
            get { yield return methodNode; }
        }
    }
}
