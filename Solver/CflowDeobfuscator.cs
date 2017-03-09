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
using System.Linq;
using System.Threading;
using de4dot.blocks;
using Microsoft.Z3;

namespace DeFlow.Solver
{
    public class CflowDeobfuscator
    {
        public List<UnsolvedBlock> UnsolvedBlocks;
        private Blocks blocks;
        private CancellationToken token;

        public void Initialize(Blocks blocks, CancellationToken token)
        {
            this.blocks = blocks;
            this.token = token;
        }

        public bool Deobfuscate()
        {
            bool result = true;

            List<Block> allBlocks = new List<Block>();

            this.CleanBlocks();

            this.blocks.MethodBlocks.GetAllBlocks(allBlocks);

            try
            {
                var solver = new CflowSolver();
                solver.Initialize(this.blocks, this.token);
                solver.Deobfuscate(allBlocks);
            }
            catch (OperationCanceledException)
            {
                result = false;
            }

            this.CleanBlocks();

            if (DeFlowSettings.Settings.Repartition)
                this.blocks.RepartitionBlocks();

            return result;
        }

        public void CheckBlocks()
        {
            List<Block> allBlocks = new List<Block>();

            this.CleanBlocks();

            this.blocks.MethodBlocks.GetAllBlocks(allBlocks);

            var solver = new CflowSolver();
            solver.Initialize(this.blocks, this.token);

            UnsolvedBlocks = new List<UnsolvedBlock>();

            foreach (var block in allBlocks)
            {
                if (block.LastInstr.IsConditionalBranch())
                {
                    if (!solver.SolveBlock(allBlocks[0], block))
                    {
                        UnsolvedBlocks.Add(new UnsolvedBlock(block, solver.FinalExpr, solver.ctx));
                    }
                }
            }
        }

        public void SolveBlockAssisted(Context ctx, BoolExpr expr, Block block)
        {
            this.CleanBlocks();

            var solver = new CflowSolver();
            solver.Initialize(this.blocks, this.token);
            solver.SolveBlockAssisted(ctx, expr, block);

            this.CleanBlocks();

            if (DeFlowSettings.Settings.Repartition)
                this.blocks.RepartitionBlocks();
        }

        public void CleanBlocks()
        {
            this.RemoveDeadBlocks();
            this.MergeBlocks();
        }

        private bool RemoveDeadBlocks()
        {
            return new DeadBlocksRemover(this.blocks.MethodBlocks).Remove() > 0;
        }

        private bool MergeBlocks()
        {
            bool modified = false;
            foreach (var scopeBlock in this.GetAllScopeBlocks(this.blocks.MethodBlocks))
                modified |= scopeBlock.MergeBlocks() > 0;
            return modified;
        }

        private IEnumerable<ScopeBlock> GetAllScopeBlocks(ScopeBlock scopeBlock)
        {
            var list = new List<ScopeBlock>();
            list.Add(scopeBlock);
            list.AddRange(scopeBlock.GetAllScopeBlocks());
            return list;
        }
    }
}
