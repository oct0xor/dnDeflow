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
using de4dot.blocks;

namespace DeFlow.Solver
{
    public class CflowCFG
    {
        public static List<List<Block>> ControlFlow = new List<List<Block>>();

        public static void GetBlocksTreePath(Block rootblock, Block leafblock)
        {
            if (rootblock == null)
            {
                return;
            }

            GetBlocksTreePath(rootblock, leafblock, new List<Block>());
        }

        internal static void GetBlocksTreePath(Block block, Block leafblock, List<Block> cfg)
        {
            bool flag = cfg.Contains(block);

            if (!flag)
                cfg.Add(block);

            if (block == leafblock)
            {
                var list = new List<Block>();

                foreach (Block path in cfg)
                    list.Add(path);

                ControlFlow.Add(list);
            }
            else if (!flag)
            {
                if (block.Targets != null)
                {
                    foreach (Block target in block.Targets)
                    {
                        GetBlocksTreePath(target, leafblock, cfg);
                    }
                }

                if (block.FallThrough != null)
                {
                    GetBlocksTreePath(block.FallThrough, leafblock, cfg);
                }
            }

            if (!flag)
                cfg.RemoveRange(cfg.Count - 1, 1);
        }
    }
}
