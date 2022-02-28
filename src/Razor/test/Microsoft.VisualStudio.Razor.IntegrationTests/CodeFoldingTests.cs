// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.CodeAnalysis.Host;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    public class CodeFoldingTests : AbstractRazorEditorTest
    {
        private async Task<ICollapsible[]> GetOutlineRegionsAsync(Text.Editor.IWpfTextView textView)
        {
            await TestServices.JoinableTaskFactory.SwitchToMainThreadAsync();
            var outliningService = await TestServices.Shell.GetComponentModelServiceAsync<IOutliningManagerService>(HangMitigatingCancellationToken);
            var manager = outliningService.GetOutliningManager(textView);
            var span = new SnapshotSpan(textView.TextSnapshot, 0, textView.TextSnapshot.Length);

            var outlines = manager.GetAllRegions(span);

            return outlines
                    .OrderBy(s => s.Extent.GetStartPoint(textView.TextSnapshot))
                    .ToArray();
        }

        private void AssertFoldableBlocks(ICollapsible[] outlines, Span[] foldableSpans, ITextView textView)
        {
            Assert.True(outlines.Length >= foldableSpans.Length);

            var spans = outlines.Select(o => o.Extent.GetSpan(textView.TextSnapshot).Span).ToImmutableArray();
            foreach (var foldableSpan in foldableSpans)
            {
                Assert.Contains(foldableSpan, spans);
            }
        }

        [IdeFact]
        public async Task CodeFolding_CodeBlock()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, CounterRazorFile, HangMitigatingCancellationToken);

            var textView = await TestServices.Editor.GetActiveTextViewAsync(HangMitigatingCancellationToken);
            var outlines = await GetOutlineRegionsAsync(textView);

            var expectedFoldableBlocks = new Span[]
            {
                Span.FromBounds(285, 324)
            };

            AssertFoldableBlocks(outlines, expectedFoldableBlocks, textView);
            outlines.First().Extent.GetSpan(textView.TextSnapshot);
        }
    }
}
