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
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Z3;

namespace DeFlow.Solver
{
    public partial class CflowSolver
    {
        public void TranslateInstruction(CflowTranslatorCtx translator, Instruction instr)
        {
            var stack = translator.Stack;
            var args = translator.Args;
            var locals = translator.Locals;

            switch (instr.OpCode.Code)
            {
                case Code.Starg:
                case Code.Starg_S: Translate_Starg(stack, args, (Parameter)instr.Operand); break;
                case Code.Stloc:
                case Code.Stloc_S: Translate_Stloc(stack, locals, (Local)instr.Operand); break;
                case Code.Stloc_0: Translate_Stloc(stack, locals, 0); break;
                case Code.Stloc_1: Translate_Stloc(stack, locals, 1); break;
                case Code.Stloc_2: Translate_Stloc(stack, locals, 2); break;
                case Code.Stloc_3: Translate_Stloc(stack, locals, 3); break;

                case Code.Ldarg:
                case Code.Ldarg_S: stack.Push(GetArg(stack, args, (Parameter)instr.Operand)); break;
                case Code.Ldarg_0: stack.Push(GetArg(stack, args, 0)); break;
                case Code.Ldarg_1: stack.Push(GetArg(stack, args, 1)); break;
                case Code.Ldarg_2: stack.Push(GetArg(stack, args, 2)); break;
                case Code.Ldarg_3: stack.Push(GetArg(stack, args, 3)); break;
                case Code.Ldloc:
                case Code.Ldloc_S: stack.Push(GetLocal(stack, locals, (Local)instr.Operand)); break;
                case Code.Ldloc_0: stack.Push(GetLocal(stack, locals, 0)); break;
                case Code.Ldloc_1: stack.Push(GetLocal(stack, locals, 1)); break;
                case Code.Ldloc_2: stack.Push(GetLocal(stack, locals, 2)); break;
                case Code.Ldloc_3: stack.Push(GetLocal(stack, locals, 3)); break;

                case Code.Ldloca:
                case Code.Ldloca_S: Translate_Ldloca(stack, locals, (Local)instr.Operand); break;

                case Code.Dup: stack.CopyTop(); break;

                case Code.Ldc_I4: stack.Push(ctx.MkBV((int)instr.Operand, 32)); break;
                case Code.Ldc_I4_S: stack.Push(ctx.MkBV((sbyte)instr.Operand, 32)); break;

                case Code.Ldc_I4_0: stack.Push(ctx.MkBV(0, 32)); break;
                case Code.Ldc_I4_1: stack.Push(ctx.MkBV(1, 32)); break;
                case Code.Ldc_I4_2: stack.Push(ctx.MkBV(2, 32)); break;
                case Code.Ldc_I4_3: stack.Push(ctx.MkBV(3, 32)); break;
                case Code.Ldc_I4_4: stack.Push(ctx.MkBV(4, 32)); break;
                case Code.Ldc_I4_5: stack.Push(ctx.MkBV(5, 32)); break;
                case Code.Ldc_I4_6: stack.Push(ctx.MkBV(6, 32)); break;
                case Code.Ldc_I4_7: stack.Push(ctx.MkBV(7, 32)); break;
                case Code.Ldc_I4_8: stack.Push(ctx.MkBV(8, 32)); break;
                case Code.Ldc_I4_M1: stack.Push(ctx.MkBV(-1, 32)); break;
                case Code.Ldnull: stack.Push(stack.Unknown()); break;
                case Code.Ldstr: stack.Push(stack.Unknown()); break;

                case Code.Add: Translate_Add(stack, instr); break;
                case Code.Sub: Translate_Sub(stack, instr); break;
                case Code.Mul: Translate_Mul(stack, instr); break;
                case Code.Div: Translate_Div(stack, instr); break;
                case Code.Div_Un: Translate_Div_Un(stack, instr); break;
                case Code.Rem: Translate_Rem(stack, instr); break;
                case Code.Rem_Un: Translate_Rem_Un(stack, instr); break;
                case Code.Neg: Translate_Neg(stack, instr); break;
                case Code.And: Translate_And(stack, instr); break;
                case Code.Or: Translate_Or(stack, instr); break;
                case Code.Xor: Translate_Xor(stack, instr); break;
                case Code.Not: Translate_Not(stack, instr); break;
                case Code.Shl: Translate_Shl(stack, instr); break;
                case Code.Shr: Translate_Shr(stack, instr); break;
                case Code.Shr_Un: Translate_Shr_Un(stack, instr); break;
                case Code.Ceq: Translate_Ceq(stack, instr); break;
                case Code.Cgt: Translate_Cgt(stack, instr); break;
                case Code.Cgt_Un: Translate_Cgt_Un(stack, instr); break;
                case Code.Clt: Translate_Clt(stack, instr); break;
                case Code.Clt_Un: Translate_Clt_Un(stack, instr); break;

                case Code.Box: stack.Push(stack.Pop()); break;
                case Code.Unbox_Any: stack.Push(stack.Pop()); break; 

                case Code.Add_Ovf: Translate_Add_Ovf(stack, instr); break;
                case Code.Add_Ovf_Un: Translate_Add_Ovf_Un(stack, instr); break;
                case Code.Sub_Ovf: Translate_Sub_Ovf(stack, instr); break;
                case Code.Sub_Ovf_Un: Translate_Sub_Ovf_Un(stack, instr); break;
                case Code.Mul_Ovf: Translate_Mul_Ovf(stack, instr); break;
                case Code.Mul_Ovf_Un: Translate_Mul_Ovf_Un(stack, instr); break;

                case Code.Ldlen: Translate_Ldlen(stack, instr); break;
                case Code.Sizeof: Translate_Sizeof(stack, instr); break;

                case Code.Ldsfld: Translate_Ldsfld(stack, instr); break;

                case Code.Ldind_I4: Translate_Ldind_I4(stack, instr); break;

                default:
                    UpdateStack(stack, instr);
                    break;
            }
        }

        private void UpdateStack(CflowStack stack, Instruction instr)
        {
            int pushes, pops;
            instr.CalculateStackUsage(out pushes, out pops);
            if (pops == -1)
                stack.Clear();
            else
            {
                stack.Pop(pops);
                stack.Push(pushes);
            }
        }

        private BitVecExpr GetUnknownArg(CflowStack stack)
        {
            return stack.Unknown() as BitVecExpr;
        }

        private void SetArg(List<BitVecExpr> args, Parameter arg, BitVecExpr value)
        {
            if (arg != null)
                SetArg(args, arg.Index, value);
        }

        private void SetArg(List<BitVecExpr> args, int index, BitVecExpr value)
        {
            if (0 <= index && index < args.Count)
                args[index] = value;
        }

        private BitVecExpr GetArg(CflowStack stack, List<BitVecExpr> args, int i)
        {
            if (0 <= i && i < args.Count)
                return args[i];

            return GetUnknownArg(stack);
        }

        private BitVecExpr GetArg(CflowStack stack, List<BitVecExpr> args, Parameter arg)
        {
            if (arg == null)
            {
                return GetUnknownArg(stack);
            }

            return GetArg(stack, args, arg.Index);
        }

        private void SetLocal(List<BitVecExpr> locals, int index, BitVecExpr value)
        {
            if (0 <= index && index < locals.Count)
                locals[index] = value;
        }

        private BitVecExpr GetUnknownLocal(CflowStack stack)
        {
            return stack.Unknown() as BitVecExpr;
        }

        private BitVecExpr GetLocal(CflowStack stack, List<BitVecExpr> locals, int i)
        {
            if (0 <= i && i < locals.Count)
                return locals[i];

            return GetUnknownLocal(stack);
        }

        private BitVecExpr GetLocal(CflowStack stack, List<BitVecExpr> locals, Local local)
        {
            if (local == null)
            {
                return GetUnknownLocal(stack);
            }

            return GetLocal(stack, locals, local.Index);
        }

        private void Translate_Add(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push(ctx.MkBVAdd(val1 as BitVecExpr, val2 as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Add_Ovf(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            { 
                stack.Push((BitVecExpr)ctx.MkITE(ctx.MkBVAddNoOverflow(val1 as BitVecExpr, val2 as BitVecExpr, true), ctx.MkBVAdd(val1 as BitVecExpr, val2 as BitVecExpr), stack.Unknown() as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Add_Ovf_Un(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push((BitVecExpr)ctx.MkITE(ctx.MkBVAddNoOverflow(val1 as BitVecExpr, val2 as BitVecExpr, false), ctx.MkBVAdd(val1 as BitVecExpr, val2 as BitVecExpr), stack.Unknown() as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Sub(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push(ctx.MkBVSub(val1 as BitVecExpr, val2 as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Sub_Ovf(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push((BitVecExpr)ctx.MkITE(ctx.MkBVSubNoUnderflow(val1 as BitVecExpr, val2 as BitVecExpr, true), ctx.MkBVSub(val1 as BitVecExpr, val2 as BitVecExpr), stack.Unknown() as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Sub_Ovf_Un(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push((BitVecExpr)ctx.MkITE(ctx.MkBVSubNoUnderflow(val1 as BitVecExpr, val2 as BitVecExpr, false), ctx.MkBVSub(val1 as BitVecExpr, val2 as BitVecExpr), stack.Unknown() as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Mul(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push(ctx.MkBVMul(val1 as BitVecExpr, val2 as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Mul_Ovf(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push((BitVecExpr)ctx.MkITE(ctx.MkBVMulNoOverflow(val1 as BitVecExpr, val2 as BitVecExpr, true), ctx.MkBVMul(val1 as BitVecExpr, val2 as BitVecExpr), stack.Unknown() as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Mul_Ovf_Un(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push((BitVecExpr)ctx.MkITE(ctx.MkBVMulNoOverflow(val1 as BitVecExpr, val2 as BitVecExpr, false), ctx.MkBVMul(val1 as BitVecExpr, val2 as BitVecExpr), stack.Unknown() as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Div(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push(ctx.MkBVSDiv(val1 as BitVecExpr, val2 as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Div_Un(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push(ctx.MkBVUDiv(val1 as BitVecExpr, val2 as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Rem(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push(ctx.MkBVSRem(val1 as BitVecExpr, val2 as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Rem_Un(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push(ctx.MkBVURem(val1 as BitVecExpr, val2 as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Neg(CflowStack stack, Instruction instr)
        {
            var val1 = stack.Pop();

            if (val1 is BitVecExpr)
                stack.Push(ctx.MkBVNeg(val1 as BitVecExpr));
            else
                stack.PushUnknown();
        }

        private void Translate_And(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push(ctx.MkBVAND(val1 as BitVecExpr, val2 as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Or(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push(ctx.MkBVOR(val1 as BitVecExpr, val2 as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Xor(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push(ctx.MkBVXOR(val1 as BitVecExpr, val2 as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Not(CflowStack stack, Instruction instr)
        {
            var val1 = stack.Pop();

            if (val1 is BitVecExpr)
                stack.Push(ctx.MkBVNot(val1 as BitVecExpr));
            else
                stack.PushUnknown();
        }

        private void Translate_Shl(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push(ctx.MkBVSHL(val1 as BitVecExpr, val2 as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Shr(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push(ctx.MkBVASHR(val1 as BitVecExpr, val2 as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Shr_Un(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && val2 is BitVecExpr)
            {
                stack.Push(ctx.MkBVLSHR(val1 as BitVecExpr, val2 as BitVecExpr));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Ceq(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && (val1 as BitVecExpr).Simplify().IsNumeral && val2 is BitVecExpr && (val2 as BitVecExpr).Simplify().IsNumeral)
            {
                stack.Push((BitVecExpr)ctx.MkITE(ctx.MkEq(val1 as BitVecExpr, val2 as BitVecExpr), ctx.MkBV(1, 32), ctx.MkBV(0, 32)));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Cgt(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && (val1 as BitVecExpr).Simplify().IsNumeral && val2 is BitVecExpr && (val2 as BitVecExpr).Simplify().IsNumeral)
            {
                stack.Push((BitVecExpr)ctx.MkITE(ctx.MkBVSGT(val1 as BitVecExpr, val2 as BitVecExpr), ctx.MkBV(1, 32), ctx.MkBV(0, 32)));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Cgt_Un(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && (val1 as BitVecExpr).Simplify().IsNumeral && val2 is BitVecExpr && (val2 as BitVecExpr).Simplify().IsNumeral)
            {
                stack.Push((BitVecExpr)ctx.MkITE(ctx.MkBVUGT(val1 as BitVecExpr, val2 as BitVecExpr), ctx.MkBV(1, 32), ctx.MkBV(0, 32)));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Clt(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && (val1 as BitVecExpr).Simplify().IsNumeral && val2 is BitVecExpr && (val2 as BitVecExpr).Simplify().IsNumeral)
            {
                stack.Push((BitVecExpr)ctx.MkITE(ctx.MkBVSLT(val1 as BitVecExpr, val2 as BitVecExpr), ctx.MkBV(1, 32), ctx.MkBV(0, 32)));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Clt_Un(CflowStack stack, Instruction instr)
        {
            var val2 = stack.Pop();
            var val1 = stack.Pop();

            if (val1 is BitVecExpr && (val1 as BitVecExpr).Simplify().IsNumeral && val2 is BitVecExpr && (val2 as BitVecExpr).Simplify().IsNumeral)
            {
                stack.Push((BitVecExpr)ctx.MkITE(ctx.MkBVULT(val1 as BitVecExpr, val2 as BitVecExpr), ctx.MkBV(1, 32), ctx.MkBV(0, 32)));
            }
            else
            {
                stack.PushUnknown();
            }
        }

        private void Translate_Starg(CflowStack stack, List<BitVecExpr> args, Parameter arg)
        {
            var val = stack.Pop();

            if (val is BitVecExpr)
                SetArg(args, arg == null ? -1 : arg.Index, val as BitVecExpr);
            else
                SetArg(args, arg == null ? -1 : arg.Index, GetUnknownArg(stack));
        }

        private void Translate_Stloc(CflowStack stack, List<BitVecExpr> locals, Local local)
        {
            Translate_Stloc(stack, locals, local == null ? -1 : local.Index);
        }

        private void Translate_Stloc(CflowStack stack, List<BitVecExpr> locals, int index)
        {
            var val = stack.Pop();

            if (val is BitVecExpr)
                SetLocal(locals, index, val as BitVecExpr);
            else
                SetLocal(locals, index, GetUnknownLocal(stack));
        }

        private void Translate_Ldsfld(CflowStack stack, Instruction instr)
        {
            stack.Push(instr.Operand as IField);
        }

        private void Translate_Ldlen(CflowStack stack, Instruction instr)
        {
            var val = stack.Pop();

            if (val is IField)
            {
                if ((val as IField).FullName == "System.Type[] System.Type::EmptyTypes")
                {
                    stack.Push(ctx.MkBV(0, 32));
                    return;
                }
            }

            stack.PushUnknown();
        }

        private void Translate_Sizeof(CflowStack stack, Instruction instr)
        {
            if (instr.Operand is TypeRef)
            {
                if ((instr.Operand as TypeRef).FullName == "System.Boolean")
                {
                    stack.Push(ctx.MkBV(sizeof(System.Boolean), 32));
                    return;
                }
                else if ((instr.Operand as TypeRef).FullName == "System.Byte")
                {
                    stack.Push(ctx.MkBV(sizeof(System.Byte), 32));
                    return;
                }
                else if ((instr.Operand as TypeRef).FullName == "System.SByte")
                {
                    stack.Push(ctx.MkBV(sizeof(System.SByte), 32));
                    return;
                }
                else if ((instr.Operand as TypeRef).FullName == "System.Char")
                {
                    stack.Push(ctx.MkBV(sizeof(System.Char), 32));
                    return;
                }
                else if ((instr.Operand as TypeRef).FullName == "System.Int16")
                {
                    stack.Push(ctx.MkBV(sizeof(System.Int16), 32));
                    return;
                }
                else if ((instr.Operand as TypeRef).FullName == "System.Int32")
                {
                    stack.Push(ctx.MkBV(sizeof(System.Int32), 32));
                    return;
                }
                else if ((instr.Operand as TypeRef).FullName == "System.Int64")
                {
                    stack.Push(ctx.MkBV(sizeof(System.Int64), 32));
                    return;
                }
                else if ((instr.Operand as TypeRef).FullName == "System.UInt16")
                {
                    stack.Push(ctx.MkBV(sizeof(System.UInt16), 32));
                    return;
                }
                else if ((instr.Operand as TypeRef).FullName == "System.UInt32")
                {
                    stack.Push(ctx.MkBV(sizeof(System.UInt32), 32));
                    return;
                }
                else if ((instr.Operand as TypeRef).FullName == "System.UInt64")
                {
                    stack.Push(ctx.MkBV(sizeof(System.UInt64), 32));
                    return;
                }
                else if ((instr.Operand as TypeRef).FullName == "System.Single")
                {
                    stack.Push(ctx.MkBV(sizeof(System.Single), 32));
                    return;
                }
                else if ((instr.Operand as TypeRef).FullName == "System.Double")
                {
                    stack.Push(ctx.MkBV(sizeof(System.Double), 32));
                    return;
                }
                else if ((instr.Operand as TypeRef).FullName == "System.Guid")
                {
                    stack.Push(ctx.MkBV(0x10, 32)); 
                    return;
                }
            }

            stack.PushUnknown();
        }

        class Address
        {
            public object Value;

            public Address(object value)
            {
                Value = value;
            }
        }

        private void Translate_Ldloca(CflowStack stack, List<BitVecExpr> locals, Local local)
        {
            stack.Push(new Address(GetLocal(stack, locals, local)));
            SetLocal(locals, local == null ? -1 : local.Index, GetUnknownLocal(stack));
        }

        private void Translate_Ldind_I4(CflowStack stack, Instruction instr)
        {
            var addr = stack.Pop();

            if (addr is Address && (addr as Address).Value is BitVecExpr)
            {
                stack.Push((addr as Address).Value);
            }
            else
            {
                stack.PushUnknown();
            }
        }
    }
}
