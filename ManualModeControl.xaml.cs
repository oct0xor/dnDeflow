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
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using de4dot.blocks;
using DeFlow.Solver;
using dnlib.DotNet;
using dnSpy.AsmEditor.UndoRedo;
using dnSpy.Contracts.App;
using dnSpy.Contracts.Controls;
using dnSpy.Contracts.Decompiler;
using dnSpy.Contracts.Documents;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Text;
using ICSharpCode.Decompiler.Disassembler;
using Microsoft.Z3;

namespace DeFlow
{
    sealed partial class ManualModeControl : WindowBase
    {
        MethodDef method;
        CflowDeobfuscator cflowDeobfuscator;
        IDecompilerOutput ilOutput;
        RichTextBoxTextColorOutput exprOutput;

        public ManualModeControl()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ilOutput = TextColorWriterToDecompilerOutput.Create(new RichTextBoxTextColorOutput(ILView, ManualMode.Theme));
            exprOutput = new RichTextBoxTextColorOutput(ExprView, ManualMode.Theme);
            ILView.Document.PageWidth = 1000;

            method = DotNetUtils.Clone(((IMethodNode)(((object[])this.DataContext)[0])).MethodDef);
            Blocks blocks = new Blocks(method);
            
            CancellationToken token = default(CancellationToken);

            cflowDeobfuscator = new CflowDeobfuscator();
            cflowDeobfuscator.Initialize(blocks, token);
            cflowDeobfuscator.CheckBlocks();

            for (int i = 0; i < cflowDeobfuscator.UnsolvedBlocks.Count(); i++)
            {
                BlocksListView.Items.Add("Block " + i.ToString());
            }

            if (BlocksListView.Items.Count > 0)
            {
                BlocksListView.SelectedIndex = 0;
            }
            else
            {
                Consts.IsEnabled = false;
                Value.IsEnabled = false;
                SetButton.IsEnabled = false;
                SolveButton.IsEnabled = false;

                MsgBox.Instance.Show("There is no unpredictable control transfers in this method");
            }
        }

        private void BlocksListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ILView.Document.Blocks.Clear();
            
            var options = new DisassemblerOptions(new System.Threading.CancellationToken(), null);
            uint rva = (uint)method.RVA;
            uint baseRva = rva == 0 ? 0 : rva + method.Body.HeaderSize;
            long baseOffs = baseRva == 0 ? 0 : method.Module.ToFileOffset(baseRva) ?? 0;
            int startLocation;
            
            foreach (var instr in cflowDeobfuscator.UnsolvedBlocks[BlocksListView.SelectedIndex].Block.Instructions)
            {         
                instr.Instruction.WriteTo(ilOutput, options, baseRva, baseOffs, null, method, out startLocation);
                ilOutput.Write("\r", BoxedTextColor.Text);
            }

            ExprView.Document.Blocks.Clear();

            var expr = cflowDeobfuscator.UnsolvedBlocks[BlocksListView.SelectedIndex].Expression;

            exprOutput.Write(TextColor.String, expr.ToString());

            Consts.Items.Clear();

            GetConsts(expr);

            Consts.IsEnabled = true;

            if (Consts.Items.Count > 0)
                Consts.SelectedIndex = 0;
            else
                Consts.IsEnabled = false;
        }

        private void SetButton_Click(object sender, RoutedEventArgs e)
        {
            Regex regex1 = new Regex("^\\d+$");
            Regex regex2 = new Regex("^-\\d+$");
            Regex regex3 = new Regex("^[A-Fa-f0-9]{1,8}$");
            Regex regex4 = new Regex("^0x[A-Fa-f0-9]{1,8}$");

            if (Consts.SelectedItem == null)
                return;

            if (regex1.IsMatch(Value.Text))
            {
                uint val = 0;
                if (uint.TryParse(Value.Text, out val))
                {
                    UpdateExpression(val);
                }
            }
            else if (regex2.IsMatch(Value.Text))
            {
                int val = 0;
                if (int.TryParse(Value.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out val))
                {
                    UpdateExpression((uint)val);
                }
            }
            else if (regex3.IsMatch(Value.Text))
            {
                uint val = 0;
                if (uint.TryParse(Value.Text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out val))
                {
                    UpdateExpression(val);
                }
            }
            else if (regex4.IsMatch(Value.Text))
            {
                uint val = 0;
                if (uint.TryParse(Value.Text.Remove(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out val))
                {
                    UpdateExpression(val);
                }
            }
        }

        private void SolveButton_Click(object sender, RoutedEventArgs e)
        {
            Lazy<IMethodAnnotations> methodAnnotations = (Lazy<IMethodAnnotations>)((object[])this.DataContext)[1];
            Lazy<IUndoCommandService> undoCommandService = (Lazy<IUndoCommandService>)((object[])this.DataContext)[2];
            IMethodNode methodNode = (IMethodNode)(((object[])this.DataContext)[0]);

            if (BlocksListView.SelectedItem == null)
                return;

            if (Consts.Items.Count > 0)
            {
                MsgBox.Instance.Show("Not all Consts are set");
                return;
            }

            var ctx = cflowDeobfuscator.UnsolvedBlocks[BlocksListView.SelectedIndex].Context;
            var expr = cflowDeobfuscator.UnsolvedBlocks[BlocksListView.SelectedIndex].Expression;
            var block = cflowDeobfuscator.UnsolvedBlocks[BlocksListView.SelectedIndex].Block;

            if (expr != null)
            {
                undoCommandService.Value.Add(new SolveBlock(methodAnnotations.Value, methodNode, ctx, expr, block));

                this.Close();
            }
        }

        private void UpdateExpression(uint value)
        {
            Expr expr = cflowDeobfuscator.UnsolvedBlocks[BlocksListView.SelectedIndex].Expression;
            var ctx = cflowDeobfuscator.UnsolvedBlocks[BlocksListView.SelectedIndex].Context;
            var val = ctx.MkBV(value, 32);

            SetConsts(expr, Consts.SelectedItem.ToString(), val);

            ExprView.Document.Blocks.Clear();

            exprOutput.Write(TextColor.String, expr.ToString());

            Consts.Items.Remove(Consts.SelectedItem);

            if (Consts.Items.Count > 0)
                Consts.SelectedIndex = 0;
            else
                Consts.IsEnabled = false;
        }

        private void GetConsts(Expr expr)
        {
            if (expr != null)
            {
                if (expr.IsConst)
                {
                    if (!Consts.Items.Contains(expr.ToString()))
                        Consts.Items.Add(expr.ToString());
                }

                foreach (var arg in expr.Args)
                    GetConsts(arg);
            }
        }

        private void SetConsts(Expr expr, string name, Expr value)
        {
            if (expr.Args.Any(x => x.IsConst && x.ToString() == name))
            {
                List<Expr> args = new List<Expr>();

                foreach (var arg in expr.Args)
                {
                    if (arg.IsConst && arg.ToString() == name)
                        args.Add(value);
                    else
                        args.Add(arg);
                }

                expr.Update(args.ToArray());
            }

            for (int i = 0; i < expr.Args.Count(); i++)
            {
                var newExpr = expr.Args[i];
                SetConsts(newExpr, name, value);

                List<Expr> args = new List<Expr>();

                for (int j = 0; j < expr.Args.Count(); j++)
                {
                    if (i == j)
                        args.Add(newExpr);
                    else
                        args.Add(expr.Args[j]);
                }

                expr.Update(args.ToArray());
            }
        }
    }
}
