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
using System.Text;
using System.Threading.Tasks;
using de4dot.blocks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Z3;

namespace DeFlow.Solver
{
    public partial class CflowSolver
    {
        private void BifurcateBlocks(List<List<Block>> cfg, List<Block> list)
        {
            if (cfg.Count() != 1)
            {
                var condInstrIndx = list.FindLastIndex(x => x.LastInstr.IsConditionalBranch());

                if (condInstrIndx != -1)
                {
                    var blocksAfterCondInstr = list.Skip(condInstrIndx + 1).ToList();

                    var blocksToClone = blocksAfterCondInstr.Take(blocksAfterCondInstr.Count() - 1); // all without last (switch)

                    foreach (var block in blocksToClone)
                    {
                        if (block.Sources.Count() > 1 && cfg.FindAll(x => x != list).Exists(x => x.Contains(block)))
                        {
                            var blockIndx = list.FindIndex(x => x == block);

                            var newBlock = new Block();

                            for (int i = 0; i < block.Instructions.Count; i++)
                            {
                                var instr = block.Instructions[i];
                                if (instr.OpCode != OpCodes.Nop)
                                    newBlock.Instructions.Add(true ? new Instr(instr.Instruction.Clone()) : instr);
                            }

                            // This newBlock should have same targets and fallthrough as original block

                            if (block.FallThrough != null)
                            {
                                newBlock.SetNewFallThrough(block.FallThrough);
                            }

                            if (block.Targets != null)
                            {
                                newBlock.Targets = new List<Block>();

                                for (int i = 0; i < block.Targets.Count; i++)
                                {
                                    newBlock.Targets.Add(new Block());
                                    newBlock.Targets[i] = block.Targets[i];
                                    block.Targets[i].Sources.Add(newBlock);
                                }
                            }

                            var blockBeforeBlock = list[blockIndx - 1];

                            if (blockBeforeBlock.FallThrough != null && blockBeforeBlock.FallThrough == block)
                            {
                                blockBeforeBlock.FallThrough.Parent.Add(newBlock);
                                blockBeforeBlock.SetNewFallThrough(newBlock);
                            }

                            if (blockBeforeBlock.Targets != null && blockBeforeBlock.Targets.Contains(block))
                            {
                                try
                                {
                                    blockBeforeBlock.Targets.Find(x => x == block).Parent.Add(newBlock);
                                    blockBeforeBlock.SetNewTarget(blockBeforeBlock.Targets.FindIndex(x => x == block), newBlock);
                                }
                                catch
                                {

                                }
                            }

                            list[blockIndx] = newBlock;
                        }
                    }
                }
            }
        }

        private bool Solve_Switch(CflowStack stack)
        {
            Block target;
            List<Block> targets;
            bool modified = false;
            int index;

            var val1 = stack.Pop();

            if (val1 is BitVecExpr && (val1 as BitVecExpr).Simplify().IsNumeral != true)
            {
                var cfg = CflowCFG.ControlFlow;

                foreach (List<Block> list in cfg)
                {
                    token.ThrowIfCancellationRequested();

                    CflowTranslatorCtx translator = TranslatorInit(true);

                    List<Instr> instructions = new List<Instr>();

                    foreach (Block b in list)
                    {
                        instructions.AddRange(b.Instructions);
                    }

                    for (int i = 0; i < instructions.Count - 1; i++)
                    {
                        var instr = instructions[i].Instruction;
                        TranslateInstruction(translator, instr);
                    }

                    object val2 = translator.Stack.Pop();

                    if (val2 is BitVecExpr && (val2 as BitVecExpr).Simplify().IsNumeral == true)
                    {
                        this.BifurcateBlocks(cfg, list);

                        if (!int.TryParse((val2 as BitVecExpr).Simplify().ToString(), out index))
                            index = -1;

                        var beforeSwitch = list[list.Count - 2];

                        if (beforeSwitch.LastInstr.IsConditionalBranch())
                        {
                            var newBlock = new Block();

                            for (int i = 0; i < list.Last().Instructions.Count - 1; i++)
                            {
                                var instr = list.Last().Instructions[i];
                                if (instr.OpCode != OpCodes.Nop)
                                    newBlock.Instructions.Add(true ? new Instr(instr.Instruction.Clone()) : instr);
                            }

                            newBlock.Insert(newBlock.Instructions.Count, OpCodes.Pop.ToInstruction()); // for switch

                            if (beforeSwitch.FallThrough != null && beforeSwitch.FallThrough == list.Last())
                            {
                                targets = block.Targets;
                                if (targets == null || index < 0 || index >= targets.Count)
                                    target = block.FallThrough;
                                else
                                    target = targets[index];

                                beforeSwitch.FallThrough.Parent.Add(newBlock);
                                beforeSwitch.SetNewFallThrough(newBlock);
                                newBlock.SetNewFallThrough(target);
                                modified = true;
                            }

                            if (beforeSwitch.Targets != null && beforeSwitch.Targets[0] == list.Last())
                            {
                                targets = block.Targets;
                                if (targets == null || index < 0 || index >= targets.Count)
                                    target = block.FallThrough;
                                else
                                    target = targets[index];

                                beforeSwitch.FallThrough.Parent.Add(newBlock);
                                beforeSwitch.SetNewTarget(0, newBlock);
                                newBlock.SetNewFallThrough(target);
                                modified = true;
                            }
                        }
                        else if (beforeSwitch.LastInstr.OpCode.Code == Code.Switch)
                        {
                            //just skip
                        }
                        else
                        {
                            var newBlock = new Block();

                            for (int i = 0; i < list.Last().Instructions.Count - 1; i++)
                            {
                                var instr = list.Last().Instructions[i];
                                if (instr.OpCode != OpCodes.Nop)
                                    newBlock.Instructions.Add(true ? new Instr(instr.Instruction.Clone()) : instr);
                            }

                            newBlock.Insert(newBlock.Instructions.Count, OpCodes.Pop.ToInstruction()); // for switch

                            targets = block.Targets;
                            if (targets == null || index < 0 || index >= targets.Count)
                                target = block.FallThrough;
                            else
                                target = targets[index];

                            beforeSwitch.FallThrough.Parent.Add(newBlock);
                            beforeSwitch.SetNewFallThrough(newBlock);
                            newBlock.SetNewFallThrough(target);
                            modified = true;
                        }

                        if (modified == true)
                            break;
                    }
                }

                return modified;
            }
            else if (val1 is BitVecExpr)
            {
                index = int.Parse((val1 as BitVecExpr).Simplify().ToString());

                targets = block.Targets;
                if (targets == null || index < 0 || index >= targets.Count)
                    target = block.FallThrough;
                else
                    target = targets[index];

                block.Insert(block.Instructions.Count - 1, OpCodes.Pop.ToInstruction());

                block.ReplaceSwitchWithBranch(target);

                return true;
            }

            return false;
        }
    }
}
