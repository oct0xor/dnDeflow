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
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Text;
using ICSharpCode.Decompiler.ILAst;

namespace DeFlow.CodeRemover
{
    public partial class ILAstDeadCode
    {
        private void GetNodes(ILNode node)
        {
            if (node is ILTryCatchBlock)
            {
                var trycatchblock = node as ILTryCatchBlock;

                ILNodes.Add(".try");
                GetNodesInBlock(trycatchblock.TryBlock);

                foreach (ILTryCatchBlock.CatchBlock block in trycatchblock.CatchBlocks)
                {
                    if (block.IsFilter)
                    {
                        ILNodes.Add("filter " + block.ExceptionVariable.Name + block.ExceptionVariable);
                    }
                    else if (block.ExceptionType != null)
                    {
                        ILNodes.Add("catch " + block.ExceptionType.FullName + " " + (block.ExceptionVariable != null ? block.ExceptionVariable.Name : string.Empty));

                        var indx = ILNodes.Where(x => x is ILExpression || (x is string && ((x as string) == "pop" || (x as string) == "prefix"))).Count();

                        var instr = Instructions[indx];

                        if (instr.OpCode == OpCodes.Pop)
                            ILNodes.Add("pop");
                    }
                    else
                    {
                        ILNodes.Add("handler " + block.ExceptionVariable.Name);
                    }

                    GetNodesInBlock(block);
                }

                if (trycatchblock.FaultBlock != null)
                {
                    ILNodes.Add("fault ");
                    GetNodesInBlock(trycatchblock.FaultBlock);
                }

                if (trycatchblock.FinallyBlock != null)
                {
                    ILNodes.Add("finally");
                    GetNodesInBlock(trycatchblock.FinallyBlock);
                }

                if (trycatchblock.FilterBlock != null)
                {
                    ILNodes.Add("filter");
                    GetNodesInBlock(trycatchblock.FilterBlock);
                }
            }
            else if (node is ILLabel)
            {
                ILNodes.Add(node);
            }
            else if (node is ILExpression)
            {
                if ((node as ILExpression).Prefixes != null)
                {
                    if ((node as ILExpression).Prefixes.Count() > 1)
                    {
                        if (DeFlowSettings.Settings.Logs)
                            OutputLog.Instance.WriteLine("Unexpected node (Prefix)");
                    }

                    foreach (var prefix in (node as ILExpression).Prefixes)
                        ILNodes.Add("prefix");
                }
                else if ((node as ILExpression).Code == ILCode.Stloc && (node as ILExpression).InferredType == null)
                {
                    var arg = (node as ILExpression).Arguments.First();

                    if (arg is ILExpression && (arg as ILExpression).Prefixes != null)
                    {
                        if ((arg as ILExpression).Prefixes.Count() > 1)
                        {
                            if (DeFlowSettings.Settings.Logs)
                                OutputLog.Instance.WriteLine("Unexpected node (Prefix)");
                        }

                        foreach (var prefix in (arg as ILExpression).Prefixes)
                            ILNodes.Add("prefix");
                    }
                }

                ILNodes.Add(node);
            }
            else
            {
                if (DeFlowSettings.Settings.Logs)
                    OutputLog.Instance.WriteLine("Unexpected node");
            }
        }

        private void GetNodesInBlock(ILBlock block)
        {
            foreach (ILNode child in block.GetChildren())
            {
                GetNodes(child);
            }
        }
    }
}
