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
using System.Text;
using System.Threading.Tasks;
using Microsoft.Z3;

namespace DeFlow.Solver
{
    public class CflowStack
    {
        List<object> stack = new List<object>();
        public Context ctx = null;
        int UnkId = 0;

        public int Size
        {
            get { return stack.Count; }
        }

        public void Initialize()
        {
            stack.Clear();
        }

        public void Clear()
        {
            stack.Clear();
        }

        public void Push(object expr)
        {
            stack.Add(expr);
        }

        public void Push(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");
            for (int i = 0; i < count; i++)
                PushUnknown();
        }

        public object Unknown()
        {
            var expr = ctx.MkBVConst("Unk" + UnkId.ToString(), 32);
            UnkId += 1;
            return expr;
        }

        public void PushUnknown()
        {
            Push(Unknown());
        }

        public object Peek()
        {
            if (stack.Count == 0)
            {
                return Unknown();
            }

            return stack[stack.Count - 1];
        }

        public object Pop()
        {
            object value = Peek();
            if (stack.Count != 0)
                stack.RemoveAt(stack.Count - 1);
            return value;
        }

        public void Pop(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");
            if (count >= stack.Count)
                stack.Clear();
            else if (count > 0)
                stack.RemoveRange(stack.Count - count, count);
        }

        public void CopyTop()
        {
            Push(Peek());
        }
    }
}
