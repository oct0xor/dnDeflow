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
using System.Linq;
using System.Threading;
using de4dot.blocks;
using DeFlow.CodeRemover;
using DeFlow.Solver;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnSpy.Contracts.Decompiler;
using Microsoft.Z3;

namespace DeFlow
{
    public static class MethodDeobfuscator
    {
        public static void Deobfuscate(MethodDef method, CancellationToken token)
        {
            bool result;
            CflowDeobfuscator cflowDeobfuscator = new CflowDeobfuscator();
            IList<Instruction> allInstructions;
            IList<ExceptionHandler> allExceptionHandlers;

            MethodDef tempMethod = DotNetUtils.Clone(method);
            Blocks blocks = new Blocks(tempMethod);
            cflowDeobfuscator.Initialize(blocks, token);
            result = cflowDeobfuscator.Deobfuscate();
            blocks.GetCode(out allInstructions, out allExceptionHandlers);
            DotNetUtils.RestoreBody(tempMethod, (IEnumerable<Instruction>)allInstructions, (IEnumerable<ExceptionHandler>)allExceptionHandlers);

            RestoreMethod(method, tempMethod);

            if (result)
                DeadCodeHandler(method, token);
        }

        public static void DeobfuscateAssisted(MethodDef method, CancellationToken token, Context ctx, BoolExpr expr, Block block)
        {
            CflowDeobfuscator cflowDeobfuscator = new CflowDeobfuscator();
            IList<Instruction> allInstructions;
            IList<ExceptionHandler> allExceptionHandlers;

            MethodDef tempMethod = DotNetUtils.Clone(method);
            Blocks blocks = new Blocks(tempMethod);
            cflowDeobfuscator.Initialize(blocks, token);

            List<Block> allBlocks = new List<Block>();
            blocks.MethodBlocks.GetAllBlocks(allBlocks);
            var newBlock = allBlocks.Find(x => x.FirstInstr.Instruction.Offset == block.FirstInstr.Instruction.Offset);

            cflowDeobfuscator.SolveBlockAssisted(ctx, expr, newBlock);
            blocks.GetCode(out allInstructions, out allExceptionHandlers);
            DotNetUtils.RestoreBody(tempMethod, (IEnumerable<Instruction>)allInstructions, (IEnumerable<ExceptionHandler>)allExceptionHandlers);

            RestoreMethod(method, tempMethod);

            DeadCodeHandler(method, token);
        }

        private static void DeadCodeHandler(MethodDef method, CancellationToken token)
        {
            List<Block> allBlocks = new List<Block>();
            IList<Instruction> allInstructions;
            IList<ExceptionHandler> allExceptionHandlers;

            ILAstDeadCode deadCodeRemover = new ILAstDeadCode();
            List<Instruction> deadInstructions = new List<Instruction>();
            deadCodeRemover.RemoveDeadCode(method, deadInstructions);

            if (!DeFlowSettings.Settings.Remove)
            {
                foreach (var instr in deadInstructions)
                {
                    uint rva = (uint)method.RVA;
                    uint baseRva = rva == 0 ? 0 : rva + method.Body.HeaderSize;
                    long baseOffs = baseRva == 0 ? 0 : method.Module.ToFileOffset(baseRva) ?? 0;
                    ulong fileOffset = (ulong)baseOffs + instr.Offset;
                    DeadInstructions.DeadInstrsList.Add(new DeadInstr((ulong)baseOffs, instr.Offset, method.Module.Location));
                }
            }
            else
            {
                var tempMethod = DotNetUtils.Clone(method);
                var blocks = new Blocks(tempMethod);

                blocks.MethodBlocks.GetAllBlocks(allBlocks);

                foreach (var block in allBlocks)
                {
                    foreach (var instr in deadInstructions)
                    {
                        var indx = block.Instructions.FindIndex(x => x.Instruction.Offset == instr.Offset && x.Instruction.OpCode == instr.OpCode && x.Instruction.Operand == instr.Operand);
                        if (indx != -1)
                        {
                            block.Instructions.RemoveAt(indx);
                            block.Instructions.Insert(indx, new Instr(OpCodes.Nop.ToInstruction()));
                        }
                    }
                }

                if (DeFlowSettings.Settings.Nops)
                {
                    foreach (Block block in allBlocks)
                    {
                        if (block.Instructions.Count() > 1)
                            block.Instructions.RemoveAll(x => x.Instruction.OpCode == OpCodes.Nop);
                    }
                }

                blocks.GetCode(out allInstructions, out allExceptionHandlers);
                DotNetUtils.RestoreBody(tempMethod, (IEnumerable<Instruction>)allInstructions, (IEnumerable<ExceptionHandler>)allExceptionHandlers);

                RestoreMethod(method, tempMethod);
            }
        }

        public static void RestoreMethod(MethodDef methodTo, MethodDef methodFrom)
        {
            var body = new CilBody();
            body.KeepOldMaxStack = methodTo.Body.KeepOldMaxStack;
            body.InitLocals = methodTo.Body.InitLocals;
            body.HeaderSize = methodTo.Body.HeaderSize;
            body.MaxStack = methodTo.Body.MaxStack;
            body.LocalVarSigTok = methodTo.Body.LocalVarSigTok;
            body.Instructions.Clear();
            foreach (var d in methodFrom.Body.Instructions)
                body.Instructions.Add(d);
            body.ExceptionHandlers.Clear();
            foreach (var d in methodFrom.Body.ExceptionHandlers)
                body.ExceptionHandlers.Add(d);
            body.Variables.Clear();
            foreach (var d in methodFrom.Body.Variables)
                body.Variables.Add(d);
            body.Scope = null;
            body.UpdateInstructionOffsets();
            methodTo.MethodBody = body;
        }
    }
}
