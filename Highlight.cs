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
using System.ComponentModel.Composition;
using System.Linq;
using dnSpy.Contracts.Documents.TreeView;
using dnSpy.Contracts.Text;
using dnSpy.Contracts.Text.Classification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace DeFlow
{
    class Highlight
    {
        [Export(typeof(ITaggerProvider))]
        [TagType(typeof(IClassificationTag))]
        [ContentType(ContentTypes.IL)]
        sealed class TextTaggerProvider : ITaggerProvider
        {
            readonly IClassificationTypeRegistryService classificationTypeRegistryService;
            readonly IAppService appService;

            [ImportingConstructor]
            TextTaggerProvider(IClassificationTypeRegistryService classificationTypeRegistryService, IAppService appService)
            {
                this.classificationTypeRegistryService = classificationTypeRegistryService;
                this.appService = appService;
            }

            public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
            {
                return new TextTagger(this.classificationTypeRegistryService, this.appService) as ITagger<T>;
            }
        }

        sealed class TextTagger : ITagger<IClassificationTag>
        {
            public event EventHandler<SnapshotSpanEventArgs> TagsChanged
            {
                add { }
                remove { }
            }

            readonly IClassificationType color;
            readonly IDocumentTreeView documentTreeView;

            public TextTagger(IClassificationTypeRegistryService classificationTypeRegistryService, IAppService appService)
            {
                this.color = classificationTypeRegistryService.GetClassificationType(ThemeClassificationTypeNames.Error);
                this.documentTreeView = appService.DocumentTreeView;
            }

            public IEnumerable<ITagSpan<IClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
            {
                if (spans.Count == 0)
                    yield break;

                if (!DeFlowSettings.Settings.Remove)
                {
                    var nodes = this.documentTreeView.TreeView.TopLevelSelection.OfType<IDocumentTreeNodeData>().ToArray();

                    if (nodes.Length == 1)
                    {
                        foreach (var span in spans)
                        {
                            var text = span.GetText();
                            if (text.Contains("IL_"))
                            {
                                if (DeadInstructions.DeadInstrsList.Exists(x => text.Contains(string.Format("0x{0:X8}", x.BaseOffs + x.InstrOffs)) && text.Contains(string.Format("IL_{0:X4}", x.InstrOffs)) && (x.Path == nodes[0].GetModule().Location)))
                                {
                                    yield return new TagSpan<IClassificationTag>(span, new ClassificationTag(this.color));
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
