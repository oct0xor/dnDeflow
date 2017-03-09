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
        private List<ILEntry> CreateILEntries()
        {
            var entries = new List<ILEntry>();

            using (var node = ILNodes.GetEnumerator())
            using (var instr = Instructions.GetEnumerator())
            {
                while (node.MoveNext())
                {
                    if (node.Current is ILExpression && !IsReAssign(node.Current as ILExpression))
                    {
                        instr.MoveNext();

                        entries.Add(new ILEntry(node.Current as ILNode, instr.Current, null, null));
                    }
                    else if (!(node.Current is string))
                    {
                        entries.Add(new ILEntry(node.Current as ILNode, null, null, null));
                    }
                    else if (node.Current is string && (node.Current as string) == "pop")
                    {
                        instr.MoveNext();
                    }
                }
            }

            return entries;
        }

        private void SetILEntriesRefs(List<ILEntry> entries, List<ILEntry> exprs)
        {
            foreach (var entry in entries)
            {
                if (entry.Node is ILExpression)
                {
                    var expr = entry.Node as ILExpression;

                    if (expr.Operand != null && expr.Operand is ILVariable)
                    {
                        var targets = entries.Where(x => x.Node is ILExpression && (x.Node as ILExpression).Arguments.Any(y => y.ContainsReferenceTo(expr.Operand as ILVariable)));

                        if (targets.Count() != 0)
                        {
                            if (entry.Targets == null)
                                entry.Targets = new List<ILEntry>();

                            foreach (var target in targets)
                            {
                                entry.Targets.Add(target);
                            }
                        }
                    }

                    var sources = exprs.Where(x => expr.Arguments.Any(y => y.ContainsReferenceTo((x.Node as ILExpression).Operand as ILVariable)));

                    if (sources.Count() != 0)
                    {
                        if (entry.Sources == null)
                            entry.Sources = new List<ILEntry>();

                        foreach (var source in sources)
                        {
                            entry.Sources.Add(source);
                        }
                    }
                }
            }
        }

        private class ILEntry
        {
            public object Node;
            public Instruction Instr;
            public List<ILEntry> Sources;
            public List<ILEntry> Targets;

            public ILEntry(object node, Instruction instr, List<ILEntry> sources, List<ILEntry> targets)
            {
                this.Node = node;
                this.Instr = instr;
                this.Sources = sources;
                this.Targets = targets;
            }
        }
    }
}
