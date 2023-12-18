﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text.Editor;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.IntegrationTests;

public class CompletionIntegrationTests(ITestOutputHelper testOutputHelper) : AbstractRazorEditorTest(testOutputHelper)
{
    private static readonly TimeSpan SnippetTimeout = TimeSpan.FromSeconds(10);

    [IdeFact]
    public async Task SnippetCompletion_Html()
    {
        await TestServices.SolutionExplorer.AddFileAsync(
            RazorProjectConstants.BlazorProjectName,
            "Test.razor",
            """
@page "Test"

<PageTitle>Test</PageTitle>

<h1>Test</h1>

@code {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
""",
            open: true,
            ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("<h1>Test</h1>", charsOffset: 1, ControlledHangMitigatingCancellationToken);
        TestServices.Input.Send("{ENTER}");
        TestServices.Input.Send("d");
        TestServices.Input.Send("d");

        

        await CommitCompletionAndVerifyAsync("""
@page "Test"

<PageTitle>Test</PageTitle>

<h1>Test</h1>
<dl>
    <dt></dt>
    <dd></dd>
</dl>

@code {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
""");
    }

    [IdeFact]
    public async Task SnippetCompletion_DoesntCommitOnSpace()
    {
        await TestServices.SolutionExplorer.AddFileAsync(
            RazorProjectConstants.BlazorProjectName,
            "Test.razor",
            """
            @page "Test"

            <PageTitle>Test</PageTitle>

            <div></div>
            """,
            open: true,
            ControlledHangMitigatingCancellationToken);

        var textView = await TestServices.Editor.GetActiveTextViewAsync(HangMitigatingCancellationToken);
        await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("<div></div>", charsOffset: 1, ControlledHangMitigatingCancellationToken);
        TestServices.Input.Send("{ENTER}");
        TestServices.Input.Send("i");
        TestServices.Input.Send("f");
        TestServices.Input.Send("r");

        // Wait until completion comes up before validating
        // that space does not commit
        await WaitForCompletionSessionAsync(textView);

        TestServices.Input.Send(" ");

        var text = textView.TextBuffer.CurrentSnapshot.GetText();

        var expected = """
            @page "Test"
            
            <PageTitle>Test</PageTitle>
            
            <div></div>
            ifr
            """;

        AssertEx.EqualOrDiff(expected, text);
    }

    [IdeFact]
    [WorkItem("https://github.com/dotnet/razor/issues/9427")]
    public async Task Snippets_DoNotTrigger_OnDelete()
    {
        await TestServices.SolutionExplorer.AddFileAsync(
            RazorProjectConstants.BlazorProjectName,
            "Test.razor",
            """
            @page "Test"

            <PageTitle>Test</PageTitle>

            <div></div>
            """,
            open: true,
            ControlledHangMitigatingCancellationToken);

        var textView = await TestServices.Editor.GetActiveTextViewAsync(HangMitigatingCancellationToken);
        await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("<di", charsOffset: 1, ControlledHangMitigatingCancellationToken);
        TestServices.Input.Send("{DELETE}");

        // Make sure completion doesn't come up for 15 seconds
        var completionSession = await WaitForCompletionSessionAsync(textView, SnippetTimeout);
        Assert.Null(completionSession);
    }

    [IdeTheory]
    [InlineData("<PageTitle")]
    [InlineData("</PageTitle")]
    [InlineData("<div")]
    [InlineData("</div")]
    [WorkItem("https://github.com/dotnet/razor/issues/9427")]
    public async Task Snippets_DoNotTrigger_InsideTag(string tag)
    {
        await TestServices.SolutionExplorer.AddFileAsync(
            RazorProjectConstants.BlazorProjectName,
            "Test.razor",
            """
            @page "Test"

            <PageTitle>Test</PageTitle>

            <div></div>
            """,
            open: true,
            ControlledHangMitigatingCancellationToken);

        var textView = await TestServices.Editor.GetActiveTextViewAsync(HangMitigatingCancellationToken);
        await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync(tag, charsOffset: 1, ControlledHangMitigatingCancellationToken);
        TestServices.Input.Send(" ");
        TestServices.Input.Send("dd");

        // Make sure completion doesn't come up for 15 seconds
        var completionSession = await WaitForCompletionSessionAsync(textView, SnippetTimeout);
        var items = completionSession?.GetComputedItems(HangMitigatingCancellationToken);

        if (items is null)
        {
            // No items to check, we're good
            return;
        }

        Assert.DoesNotContain("dd", items.Items.Select(i => i.DisplayText));
    }

    [IdeFact, WorkItem("https://github.com/dotnet/razor/issues/9346")]
    public async Task Completion_EnumDot()
    {
        await TestServices.SolutionExplorer.AddFileAsync(
            RazorProjectConstants.BlazorProjectName,
            "Test.razor",
            """
            <Test Param="@MyEnum." />

            @code {
                [Parameter] public string Param { get; set; }

                public enum MyEnum
                {
                    One
                }
            }
            """,
            open: true,
            ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("@MyEnum.", charsOffset: 1, ControlledHangMitigatingCancellationToken);
        TestServices.Input.Send("O");

        await CommitCompletionAndVerifyAsync("""
            <Test Param="@MyEnum.One" />
            
            @code {
                [Parameter] public string Param { get; set; }
            
                public enum MyEnum
                {
                    One
                }
            }
            """);
    }

    private async Task CommitCompletionAndVerifyAsync(string expected)
    {
        var textView = await TestServices.Editor.GetActiveTextViewAsync(HangMitigatingCancellationToken);
        var session = await WaitForCompletionSessionAsync(textView);

        Assert.NotNull(session);
        Assert.True(session.CommitIfUnique(HangMitigatingCancellationToken));

        var text = textView.TextBuffer.CurrentSnapshot.GetText();

        // Snippets may have slight whitespace differences due to line endings. These
        // tests allow for it as long as the content is correct
        AssertEx.AssertEqualToleratingWhitespaceDifferences(expected, text);
    }

    private async Task<IAsyncCompletionSession?> WaitForCompletionSessionAsync(IWpfTextView textView, TimeSpan? timeOut = null)
    {
        var stopWatch = Stopwatch.StartNew();
        var asyncCompletion = await TestServices.Shell.GetComponentModelServiceAsync<IAsyncCompletionBroker>(HangMitigatingCancellationToken);
        var session = asyncCompletion.TriggerCompletion(textView, new CompletionTrigger(CompletionTriggerReason.Insertion, textView.TextSnapshot), textView.Caret.Position.BufferPosition, HangMitigatingCancellationToken);

        // Loop until completion comes up
        while (session is null || session.IsDismissed)
        {
            if (timeOut is not null && stopWatch.ElapsedMilliseconds >= timeOut.Value.TotalMilliseconds)
            {
                return null;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), HangMitigatingCancellationToken);
            session = asyncCompletion.TriggerCompletion(textView, new CompletionTrigger(CompletionTriggerReason.Insertion, textView.TextSnapshot), textView.Caret.Position.BufferPosition, HangMitigatingCancellationToken);
        }

        return session;
    }
}
