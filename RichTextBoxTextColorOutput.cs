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
using System.Windows.Controls;
using System.Windows.Documents;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.Themes;

namespace DeFlow
{
    public sealed class RichTextBoxTextColorOutput : ITextColorWriter
    {
        readonly RichTextBox box;
        readonly ITheme theme;

        public bool IsEmpty => new TextRange(box.Document.ContentStart, box.Document.ContentEnd).Text.Length == 0;

        public string Text => new TextRange(box.Document.ContentStart, box.Document.ContentEnd).Text;

        public RichTextBoxTextColorOutput(RichTextBox box, ITheme theme)
        {
            this.box = box;
            this.theme = theme;
        }

        public void Write(object color, string text)
        {
            if (color is TextColor)
            {
                Write((TextColor)color, text);
            }
        }

        public void Write(TextColor color, string text)
        {
            TextRange textRange = new TextRange(box.Document.ContentEnd, box.Document.ContentEnd);
            textRange.Text = text;
            textRange.ApplyPropertyValue(TextElement.ForegroundProperty, theme.GetTextColor((color).ToColorType()).Foreground);
        }

        public override string ToString()
        {
            return new TextRange(box.Document.ContentStart, box.Document.ContentEnd).Text;
        }
    }
}
