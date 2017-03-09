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
using System.Threading;
using dnSpy.Contracts.Text;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Z3;

namespace DeFlow.Solver
{
    public partial class CflowSolver
    {
        public Context ctx;
        protected Blocks blocks;
        private Block block;
        private CancellationToken token;

        public virtual void Initialize(Blocks blocks, CancellationToken token)
        {
            this.blocks = blocks;
            this.token = token;
        }

        public void Deobfuscate(List<Block> allBlocks)
        {
            var sources = new List<Block>();
            sources.Add(allBlocks[0]);
            sources.AddRange(allBlocks.Where(x => x.Sources.Count() == 0));

            bool modified;
            foreach (var source in sources.Distinct())
            {
                foreach (var block in allBlocks)
                {
                    try
                    {
                        do
                        {
                            modified = false;

                            if (block.LastInstr.OpCode.Code == Code.Switch)
                            {
                                CflowCFG.ControlFlow.Clear();
                                CflowCFG.GetBlocksTreePath(source, block);

                                if (DeFlowSettings.Settings.Logs)
                                    OutputLog.Instance.WriteLine("CFG: " + CflowCFG.ControlFlow.Count().ToString());
                            }

                            token.ThrowIfCancellationRequested();

                            modified = this.SolveBlock(source, block);
                        }
                        while (modified);
                    }
                    catch (OperationCanceledException)
                    {
                        token.ThrowIfCancellationRequested();
                    }
                    catch (Exception ex)
                    {
                        Debug.Fail(ex.ToString());
                    }
                }
            }
        }

        public bool SolveBlock(Block source, Block block)
        {
            this.block = block;

            if (!block.LastInstr.IsConditionalBranch() && block.LastInstr.OpCode.Code != Code.Switch)
                return false;

            ctx = new Context();

            CflowTranslatorCtx translator = this.TranslatorInit(source == block && !(source.Parent is HandlerBlock));

            var instructions = block.Instructions;
            if (instructions.Count == 0)
                return false;

            try
            {
                for (int i = 0; i < instructions.Count - 1; i++)
                {
                    var instr = instructions[i].Instruction;
                    TranslateInstruction(translator, instr);
                }
            }
            catch (NullReferenceException)
            {
                return false;
            }

            return TranslateBranch(translator);
        }

        public bool SolveBlockAssisted(Context ctx, BoolExpr expr, Block block)
        {
            this.block = block;
            return SolveBranchAssisted(ctx, expr);
        }

        private CflowTranslatorCtx TranslatorInit(bool fromFirstBlock)
        {
            ParameterList methodParameters = this.blocks.Method.Parameters;
            LocalList methodLocals = this.blocks.Method.Body.Variables;

            CflowTranslatorCtx translator = new CflowTranslatorCtx(ctx, new CflowStack(), new List<BitVecExpr>(), new List<BitVecExpr>());

            for (int i = 0; i < methodParameters.Count; i++)
            {
                translator.Args.Add(ctx.MkBVConst("Arg" + i.ToString(), 32));
            }

            for (int i = 0; i < methodLocals.Count; i++)
            {
                if (fromFirstBlock)
                    translator.Locals.Add(ctx.MkBV(0, 32));
                else
                    translator.Locals.Add(ctx.MkBVConst("Loc" + i.ToString(), 32));
            }

            return translator;
        }
    }
}
