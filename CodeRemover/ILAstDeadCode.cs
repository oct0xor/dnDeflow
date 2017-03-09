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
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.ILAst;

namespace DeFlow.CodeRemover
{
    public partial class ILAstDeadCode
    {
        private List<object> ILNodes = new List<object>();
        private ILBlock ILMethod = new ILBlock();
        private List<Instruction> Instructions;

        public bool RemoveDeadCode(MethodDef method, List<Instruction> dead)
        {
            ILAstBuilder astBuilder = new ILAstBuilder();
            bool inlineVariables = true;

            if (!method.HasBody)
            {
                return false;
            }

            DecompilerContext context = new DecompilerContext(method.Module) { CurrentType = method.DeclaringType, CurrentMethod = method };

            try
            {
                ILMethod.Body = astBuilder.Build(method, inlineVariables, context);
            }
            catch (Exception ex)
            {
                if (DeFlowSettings.Settings.Logs)
                    OutputLog.Instance.WriteLine("Failed to de-obfuscate method: " + method.MDToken.Raw.ToString());
                return false;
            }

            Instructions = method.Body.Instructions.Where(x => x.OpCode.Code != Code.Constrained).ToList();

            foreach (ILNode node in ILMethod.Body)
                GetNodes(node);

            if (ILNodes.Count != method.Body.Instructions.Count() + ILNodes.Where(x => x is ILLabel).Count() + ILNodes.Where(x => x is string && (x as string) != "pop" && (x as string) != "prefix").Count() 
                + ILNodes.Where(x => x is ILExpression && IsReAssign(x as ILExpression)).Count())
            {
                if (DeFlowSettings.Settings.Logs)
                    OutputLog.Instance.WriteLine("ilNodes is wrong: " + string.Format("0x{0:X8}", method.MDToken.Raw));
                return false;
            }

            var entries = CreateILEntries();
            var exprs = entries.Where(x => x.Node is ILExpression && (x.Node as ILExpression).Operand != null && (x.Node as ILExpression).Operand is ILVariable).ToList();
            var catchs = ILNodes.Where(y => y is string && (y as string).Contains("catch"));
            var rets = entries.Where(x => x.Instr != null && x.Instr.OpCode.Code == Code.Ret);

            SetILEntriesRefs(entries, exprs);

            bool modified;

            do
            {
                modified = false;

                var unused = exprs.Where(x => x.Targets == null || x.Targets.All(y => y.Instr != null && y.Instr.OpCode == OpCodes.Pop)).ToList();

                foreach (var expr in unused)
                {
                    if (expr.Instr != null)
                    {
                        // If its func call without returned value, we cant delete it.
                        if (!CallInstruction(expr.Instr.OpCode.Code))
                        {
                            // If its func call with returned value we dont want to delete this returned value too even if its unused.
                            if (!(expr.Sources != null && expr.Sources.Any(x => CallInstruction(x.Instr.OpCode.Code))))
                            {
                                // Exception Variable
                                if (!(expr.Node as ILExpression).Arguments.Any(x => x.Operand is ILVariable && catchs.Any(y => (y as string).Contains((x.Operand as ILVariable).Name))))
                                {
                                    // Dups
                                    List<ILEntry> path = new List<ILEntry>();
                                    if (!(expr.Instr.OpCode.Code != Code.Dup && CheckDupSources(expr, path)))
                                    {
                                        modified = true;

                                        dead.Add(expr.Instr);

                                        foreach (var source in exprs.Where(x => x.Targets != null && x.Targets.Any(y => y == expr)).ToList())
                                        {
                                            source.Targets = source.Targets.Where(x => x != expr).ToList();
                                        }

                                        exprs.Remove(expr);

                                        if (expr.Targets != null)
                                        {
                                            foreach (var pop in expr.Targets)
                                            {
                                                dead.Add(pop.Instr);
                                                exprs.Remove(pop);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            while (modified);

            return true;
        }

        bool CheckDupSources(ILEntry expr, List<ILEntry> path)
        {
            bool ret = false;
            bool flag = path.Contains(expr) || path.Count() > 10;

            if (!flag)
                path.Add(expr);

            if (!flag && expr != null && expr.Sources != null)
            {
                foreach (var source in expr.Sources)
                {
                    if (source != null)
                    {
                        if (source.Instr.OpCode.Code == Code.Dup)
                        {
                            ret = CheckDupTargets(source);
                        }
                        else
                        {
                            ret = CheckDupSources(source, path);
                        }

                        if (ret == true)
                            break;
                    }
                }
            }

            if (!flag)
                path.RemoveRange(path.Count - 1, 1);

            return ret;
        }

        bool CheckDupTargets(ILEntry expr)
        {
            bool ret = false;

            if (expr != null && expr.Targets != null)
            {
                foreach (var target in expr.Targets)
                {
                    if (target != null)
                    {
                        if (CallInstruction(target.Instr.OpCode.Code) || target.Instr.OpCode.Code == Code.Ret)
                        {
                            return true;
                        }
                    }
                }
            }

            return ret;
        }
    }
}
