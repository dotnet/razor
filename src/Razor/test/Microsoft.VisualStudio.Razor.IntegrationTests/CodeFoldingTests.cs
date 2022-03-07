// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
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

        private async Task AssertFoldableBlocksAsync(params string[] blockTexts)
        {
            var textView = await TestServices.Editor.GetActiveTextViewAsync(HangMitigatingCancellationToken);
            var text = textView.TextBuffer.CurrentSnapshot.GetText();

            var foldableSpans = blockTexts.Select(blockText =>
            {
                Assert.Contains(blockText, text);
                var start = text.IndexOf(blockText);
                return new Span(start, blockText.Length);
            }).ToImmutableArray();

            //
            // Built in retry logic because getting spans can take time.
            //
            var tries = 0;
            const int MaxTries = 20;
            ImmutableArray<Span> missingSpans;
            while (tries++ < MaxTries)
            {
                var outlines = await GetOutlineRegionsAsync(textView);

                missingSpans = GetMissingExpectedSpans(outlines, foldableSpans, textView);
                if (missingSpans.Length == 0)
                {
                    return;
                }

                await Task.Delay(500);
            }

            Assert.Empty(missingSpans);

            static ImmutableArray<Span> GetMissingExpectedSpans(ICollapsible[] outlines, ImmutableArray<Span> foldableSpans, ITextView textView)
            {
                if (outlines.Length < foldableSpans.Length)
                {
                    return foldableSpans.ToImmutableArray();
                }

                var builder = new List<Span>();

                var spans = outlines.Select(o => o.Extent.GetSpan(textView.TextSnapshot).Span).ToImmutableArray();
                foreach (var foldableSpan in foldableSpans)
                {
                    if (!spans.Contains(foldableSpan))
                    {
                        builder.Add(foldableSpan);
                    }
                }

                return builder.ToImmutableArray();
            }
        }

        [IdeFact]
        public async Task CodeFolding_CodeBlock_Default()
        {
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, CounterRazorFile, HangMitigatingCancellationToken);

            await AssertFoldableBlocksAsync(
@"@code {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}");
        }

        [IdeFact]
        public async Task CodeFolding_IfBlock()
        {
            await TestServices.SolutionExplorer.AddFileAsync(
                BlazorProjectName,
                "Test.razor",
                @"
@page ""/Test""

<PageTitle>Test</PageTitle>

<h1>Test</h1>

@if(true)
{
    if (true)
    {
        M();
    }
}

@code {
    string M() => ""M"";
}
",
                open: true,
                HangMitigatingCancellationToken);

            await AssertFoldableBlocksAsync(
@"@if(true)
{
    if (true)
    {
        M();
    }
}
",
@"if (true)
{
    M();
}
",
@"@code {
    string M() => ""M"";
}");
        }

        [IdeFact]
        public async Task CodeFolding_ForEach()
        {
            await TestServices.SolutionExplorer.AddFileAsync(
                BlazorProjectName,
                "Test.razor",
                @"
@page ""/Test""

<PageTitle>Test</PageTitle>

<h1>Test</h1>

@foreach (var s in GetStuff())
{
    <h2>s</h2>
}

@code {
    string[] GetStuff() => new string[0];
}
",
                open: true,
                HangMitigatingCancellationToken);

            await AssertFoldableBlocksAsync(
@"@foreach (var s in GetStuff())
{
    <h2>s</h2>
}");
        }

        [IdeFact]
        public async Task CodeFolding_CodeBlock_Region()
        {
            await TestServices.SolutionExplorer.AddFileAsync(
                BlazorProjectName,
                "Test.razor",
                @"
@page ""/Test""

<PageTitle>Test</PageTitle>

<h1>Test</h1>

@code {
    #region Methods
    void M1() { }
    void M2() { }
    #endregion
}
",
                open: true,
                HangMitigatingCancellationToken);

            await AssertFoldableBlocksAsync(@"#region Methods
    void M1() { }
    void M2() { }
    #endregion");
        }

    }
}
