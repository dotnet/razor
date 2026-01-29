// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudioCode.Razor.E2ETests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.E2ETests.Scenarios;

/// <summary>
/// E2E tests for formatting in Razor files.
/// </summary>
public class FormattingTests(ITestOutputHelper output) : VSCodeIntegrationTestBase(output)
{
    [Fact]
    public async Task FormatDocument_RazorFile_FormatsSuccessfully()
    {
        // Arrange
        await OpenFileAndWaitForReadyAsync("BlazorApp/Components/Pages/Home.razor");

        // Act
        var formattingWorked = await Razor.VerifyFormattingAsync();

        // Assert
        Assert.True(formattingWorked, "Expected formatting to produce output");
    }

    [Fact]
    public async Task FormatDocument_CshtmlFile_FormatsSuccessfully()
    {
        // Arrange
        await OpenFileAndWaitForReadyAsync("RazorPagesApp/Pages/Index.cshtml");

        // Act
        var formattingWorked = await Razor.VerifyFormattingAsync();

        // Assert
        Assert.True(formattingWorked, "Expected formatting to produce output for .cshtml file");
    }

    [Fact]
    public async Task FormatDocument_MalformedHtml_HandlesGracefully()
    {
        // Arrange
        await OpenFileAndWaitForReadyAsync("BlazorApp/Components/Pages/Home.razor");

        // Add some malformed HTML
        await Editor.GoToLineAsync(10);
        await Editor.TypeAsync("<div><span></div></span>");

        // Act - formatting should not crash
        await Editor.FormatDocumentAsync();

        // Assert - just verify we can still get text (didn't crash)
        var text = await Editor.GetEditorTextAsync();
        Assert.NotNull(text);

        // Clean up
        await Editor.UndoAsync();
    }
}
