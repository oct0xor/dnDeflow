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
using dnlib.DotNet.Emit;
using ICSharpCode.Decompiler.ILAst;

namespace DeFlow.CodeRemover
{
    public partial class ILAstDeadCode
    {
        private bool CallInstruction(Code code)
        {
            switch (code)
            {
                case Code.Call:
                case Code.Calli:
                case Code.Callvirt:
                    return true;

                default:
                    return false;
            }
        }

        private bool IsReAssign(ILExpression expr)
        {
            if (expr.Operand is ILVariable && (expr.Operand as ILVariable).GeneratedByDecompiler)
            {
                if (expr.Code == ILCode.Stloc && expr.InferredType == null)
                {
                    if (expr.Arguments.Count() == 1 && expr.Arguments[0].Code == ILCode.Ldloc)
                    {
                        if (expr.Arguments[0].Operand is ILVariable && (expr.Arguments[0].Operand as ILVariable).GeneratedByDecompiler)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
