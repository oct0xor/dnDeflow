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
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Z3;

namespace DeFlow.Solver
{
    public partial class CflowSolver
    {
        public BoolExpr FinalExpr;

        public bool TranslateBranch(CflowTranslatorCtx translator)
        {
            var stack = translator.Stack;
            Instruction instr = block.LastInstr.Instruction;

            switch (instr.OpCode.Code)
            {
                case Code.Beq:
                case Code.Beq_S: return Solve_Beq(stack);
                case Code.Bge:
                case Code.Bge_S: return Solve_Bge(stack);
                case Code.Bge_Un:
                case Code.Bge_Un_S: return Solve_Bge_Un(stack);
                case Code.Bgt:
                case Code.Bgt_S: return Solve_Bgt(stack);
                case Code.Bgt_Un:
                case Code.Bgt_Un_S: return Solve_Bgt_Un(stack);
                case Code.Ble:
                case Code.Ble_S: return Solve_Ble(stack);
                case Code.Ble_Un:
                case Code.Ble_Un_S: return Solve_Ble_Un(stack);
                case Code.Blt:
                case Code.Blt_S: return Solve_Blt(stack);
                case Code.Blt_Un:
                case Code.Blt_Un_S: return Solve_Blt_Un(stack);
                case Code.Bne_Un:
                case Code.Bne_Un_S: return Solve_Bne_Un(stack);
                case Code.Brfalse:
                case Code.Brfalse_S: return Solve_Brfalse(stack);
                case Code.Brtrue:
                case Code.Brtrue_S: return Solve_Brtrue(stack);
                case Code.Switch: return Solve_Switch(stack);

                default:
                    return false;
            }
        }

        public bool SolveBranchAssisted(Context ctx, BoolExpr expr)
        {
            Instruction instr = block.LastInstr.Instruction;

            if (instr.OpCode.Code == Code.Brfalse || instr.OpCode.Code == Code.Brfalse_S)
            {
                Microsoft.Z3.Solver s = ctx.MkSolver();
                s.Assert(expr);
                if (s.Check() == Status.SATISFIABLE)
                {
                    this.PopPushedArgs(1);

                    block.ReplaceBccWithBranch(true);

                    return true;
                }
                else
                {
                    this.PopPushedArgs(1);

                    block.ReplaceBccWithBranch(false);

                    return true;
                }
            }
            else if (instr.OpCode.Code == Code.Brtrue || instr.OpCode.Code == Code.Brtrue_S)
            {
                Microsoft.Z3.Solver s = ctx.MkSolver();
                s.Assert(expr);
                if (s.Check() == Status.UNSATISFIABLE)
                {
                    this.PopPushedArgs(1);

                    block.ReplaceBccWithBranch(true);

                    return true;
                }
                else
                {
                    this.PopPushedArgs(1);

                    block.ReplaceBccWithBranch(false);

                    return true;
                }
            }
            else
            {
                Microsoft.Z3.Solver s1 = ctx.MkSolver();
                Microsoft.Z3.Solver s2 = ctx.MkSolver();

                s1.Add(expr);
                s2.Add(ctx.MkNot(expr));

                return this.CheckBranch(s1, s2);
            }
        }

        private bool CheckBranch(Microsoft.Z3.Solver s1, Microsoft.Z3.Solver s2)
        {
            if (s1.Check() == Status.UNSATISFIABLE)
            {
                // opaque predicate not taken
                this.PopPushedArgs(2);

                block.ReplaceBccWithBranch(false);

                return true;
            }
            else if (s2.Check() == Status.UNSATISFIABLE)
            {
                // opaque predicate taken
                this.PopPushedArgs(2);

                block.ReplaceBccWithBranch(true);

                return true;
            }
            else
            {
                return false;
            }
        }

        private bool Solve_Beq(CflowStack stack)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                FinalExpr = ctx.MkEq(val1 as BitVecExpr, val2 as BitVecExpr);

                if ((val1 as BitVecExpr).Simplify().IsNumeral && (val2 as BitVecExpr).Simplify().IsNumeral)
                {
                    Microsoft.Z3.Solver s1 = ctx.MkSolver();
                    Microsoft.Z3.Solver s2 = ctx.MkSolver();

                    s1.Add(FinalExpr);
                    s2.Add(ctx.MkNot(FinalExpr));

                    return this.CheckBranch(s1, s2);
                }
            }

            return false;
        }

        private bool Solve_Bge(CflowStack stack)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                FinalExpr = ctx.MkBVSGE(val1 as BitVecExpr, val2 as BitVecExpr);

                if ((val1 as BitVecExpr).Simplify().IsNumeral && (val2 as BitVecExpr).Simplify().IsNumeral)
                {
                    Microsoft.Z3.Solver s1 = ctx.MkSolver();
                    Microsoft.Z3.Solver s2 = ctx.MkSolver();

                    s1.Add(FinalExpr);
                    s2.Add(ctx.MkNot(FinalExpr));

                    return this.CheckBranch(s1, s2);
                }
            }

            return false;
        }

        private bool Solve_Bge_Un(CflowStack stack)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                FinalExpr = ctx.MkBVUGE(val1 as BitVecExpr, val2 as BitVecExpr);

                if ((val1 as BitVecExpr).Simplify().IsNumeral && (val2 as BitVecExpr).Simplify().IsNumeral)
                {
                    Microsoft.Z3.Solver s1 = ctx.MkSolver();
                    Microsoft.Z3.Solver s2 = ctx.MkSolver();

                    s1.Add(FinalExpr);
                    s2.Add(ctx.MkNot(FinalExpr));

                    return this.CheckBranch(s1, s2);
                }
            }

            return false;
        }

        private bool Solve_Bgt(CflowStack stack)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                FinalExpr = ctx.MkBVSGT(val1 as BitVecExpr, val2 as BitVecExpr);

                if ((val1 as BitVecExpr).Simplify().IsNumeral && (val2 as BitVecExpr).Simplify().IsNumeral)
                {
                    Microsoft.Z3.Solver s1 = ctx.MkSolver();
                    Microsoft.Z3.Solver s2 = ctx.MkSolver();

                    s1.Add(FinalExpr);
                    s2.Add(ctx.MkNot(FinalExpr));

                    return this.CheckBranch(s1, s2);
                }
            }

            return false;
        }

        private bool Solve_Bgt_Un(CflowStack stack)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                FinalExpr = ctx.MkBVUGT(val1 as BitVecExpr, val2 as BitVecExpr);

                if ((val1 as BitVecExpr).Simplify().IsNumeral && (val2 as BitVecExpr).Simplify().IsNumeral)
                {
                    Microsoft.Z3.Solver s1 = ctx.MkSolver();
                    Microsoft.Z3.Solver s2 = ctx.MkSolver();

                    s1.Add(FinalExpr);
                    s2.Add(ctx.MkNot(FinalExpr));

                    return this.CheckBranch(s1, s2);
                }
            }

            return false;
        }

        private bool Solve_Ble(CflowStack stack)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                FinalExpr = ctx.MkBVSLE(val1 as BitVecExpr, val2 as BitVecExpr);

                if ((val1 as BitVecExpr).Simplify().IsNumeral && (val2 as BitVecExpr).Simplify().IsNumeral)
                {
                    Microsoft.Z3.Solver s1 = ctx.MkSolver();
                    Microsoft.Z3.Solver s2 = ctx.MkSolver();

                    s1.Add(FinalExpr);
                    s2.Add(ctx.MkNot(FinalExpr));

                    return this.CheckBranch(s1, s2);
                }
            }

            return false;
        }

        private bool Solve_Ble_Un(CflowStack stack)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                FinalExpr = ctx.MkBVULE(val1 as BitVecExpr, val2 as BitVecExpr);

                if ((val1 as BitVecExpr).Simplify().IsNumeral && (val2 as BitVecExpr).Simplify().IsNumeral)
                {
                    Microsoft.Z3.Solver s1 = ctx.MkSolver();
                    Microsoft.Z3.Solver s2 = ctx.MkSolver();

                    s1.Add(FinalExpr);
                    s2.Add(ctx.MkNot(FinalExpr));

                    return this.CheckBranch(s1, s2);
                }
            }

            return false;
        }

        private bool Solve_Blt(CflowStack stack)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                FinalExpr = ctx.MkBVSLT(val1 as BitVecExpr, val2 as BitVecExpr);

                if ((val1 as BitVecExpr).Simplify().IsNumeral && (val2 as BitVecExpr).Simplify().IsNumeral)
                {
                    Microsoft.Z3.Solver s1 = ctx.MkSolver();
                    Microsoft.Z3.Solver s2 = ctx.MkSolver();

                    s1.Add(FinalExpr);
                    s2.Add(ctx.MkNot(FinalExpr));

                    return this.CheckBranch(s1, s2);
                }
            }

            return false;
        }

        private bool Solve_Blt_Un(CflowStack stack)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                FinalExpr = ctx.MkBVULT(val1 as BitVecExpr, val2 as BitVecExpr);

                if ((val1 as BitVecExpr).Simplify().IsNumeral && (val2 as BitVecExpr).Simplify().IsNumeral)
                {
                    Microsoft.Z3.Solver s1 = ctx.MkSolver();
                    Microsoft.Z3.Solver s2 = ctx.MkSolver();

                    s1.Add(FinalExpr);
                    s2.Add(ctx.MkNot(FinalExpr));

                    return this.CheckBranch(s1, s2);
                }
            }

            return false;
        }

        private bool Solve_Bne_Un(CflowStack stack)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                FinalExpr = ctx.MkNot(ctx.MkEq(val1 as BitVecExpr, val2 as BitVecExpr));

                if ((val1 as BitVecExpr).Simplify().IsNumeral && (val2 as BitVecExpr).Simplify().IsNumeral)
                {
                    Microsoft.Z3.Solver s1 = ctx.MkSolver();
                    Microsoft.Z3.Solver s2 = ctx.MkSolver();

                    s1.Add(FinalExpr);
                    s2.Add(ctx.MkNot(FinalExpr));

                    return this.CheckBranch(s1, s2);
                }
            }

            return false;
        }

        private bool Solve_Brfalse(CflowStack stack)
        {
            var val1 = stack.Pop();

            if (val1 is BitVecExpr)
            {
                FinalExpr = ctx.MkEq(val1 as BitVecExpr, ctx.MkBV(0, 32));

                if ((val1 as BitVecExpr).Simplify().IsNumeral)
                {
                    Microsoft.Z3.Solver s = ctx.MkSolver();
                    s.Assert(FinalExpr);
                    if (s.Check() == Status.SATISFIABLE)
                    {
                        this.PopPushedArgs(1);

                        block.ReplaceBccWithBranch(true);

                        return true;
                    }
                    else
                    {
                        this.PopPushedArgs(1);

                        block.ReplaceBccWithBranch(false);

                        return true;
                    }
                }
            }

            return false;
        }

        private bool Solve_Brtrue(CflowStack stack)
        {
            var val1 = stack.Pop();

            if (val1 is BitVecExpr)
            {
                FinalExpr = ctx.MkEq(val1 as BitVecExpr, ctx.MkBV(0, 32));

                if ((val1 as BitVecExpr).Simplify().IsNumeral)
                {
                    Microsoft.Z3.Solver s = ctx.MkSolver();
                    s.Assert(FinalExpr);
                    if (s.Check() == Status.UNSATISFIABLE)
                    {
                        this.PopPushedArgs(1);

                        block.ReplaceBccWithBranch(true);

                        return true;
                    }
                    else
                    {
                        this.PopPushedArgs(1);

                        block.ReplaceBccWithBranch(false);

                        return true;
                    }
                }
            }

            return false;
        }

        private void PopPushedArgs(int stackArgs)
        {
            for (int i = 0; i < stackArgs; i++)
                block.Insert(block.Instructions.Count - 1, OpCodes.Pop.ToInstruction());
        }
    }
}
