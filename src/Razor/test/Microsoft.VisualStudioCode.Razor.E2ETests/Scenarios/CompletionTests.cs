// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudioCode.Razor.E2ETests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.E2ETests.Scenarios;

/// <summary>
/// E2E tests for IntelliSense completion in Razor files.
/// </summary>
public class CompletionTests(ITestOutputHelper output) : VSCodeIntegrationTestBase(output)
{
    [Fact]
    public async Task CSharpCompletion_InCodeBlock_ShowsCSharpItems()
    {
        // Arrange
        await OpenFileAsync("BlazorApp/Components/Pages/Home.razor");

        // Navigate to the @code block
        await Editor.GoToLineAsync(20); // Inside the @code block

        // Act & Assert
        var hasCSharpCompletions = await Razor.VerifyCSharpCompletionInCodeBlockAsync();
        Assert.True(hasCSharpCompletions, "Expected C# completions in @code block");
    }

    [Fact]
    public async Task HtmlCompletion_InMarkup_ShowsHtmlElements()
    {
        // Arrange
        await OpenFileAsync("BlazorApp/Components/Pages/Home.razor");

        // Navigate to the HTML section
        await Editor.GoToLineAsync(10); // In the HTML markup area
        await Editor.TypeAsync("<");

        // Act & Assert
        var hasHtmlCompletions = await Razor.VerifyHtmlCompletionAsync();
        Assert.True(hasHtmlCompletions, "Expected HTML element completions");

        await Editor.UndoAsync();
    }

    [Fact]
    public async Task RazorDirectiveCompletion_AfterAt_ShowsDirectives()
    {
        // Arrange
        await OpenFileAsync("BlazorApp/Components/Pages/Home.razor");

        // Navigate to a good position for directives
        await Editor.GoToLineAsync(5);

        // Act & Assert
        var hasRazorDirectives = await Razor.VerifyRazorDirectiveCompletionAsync();
        Assert.True(hasRazorDirectives, "Expected Razor directive completions after @");
    }

    [Fact]
    public async Task ComponentParameterCompletion_OnComponent_ShowsParameters()
    {
        // Arrange
        await OpenFileAsync("BlazorApp/Components/Pages/Home.razor");

        // Navigate to a good position for adding a component
        await Editor.GoToLineAsync(12);

        // Act & Assert
        var hasParameterCompletions = await Razor.VerifyComponentParameterCompletionAsync("Counter");
        Assert.True(hasParameterCompletions, "Expected component parameter completions");
    }
}
