// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudioCode.Razor.E2ETests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.E2ETests.Scenarios;

/// <summary>
/// E2E tests for Go to Definition in Razor files.
/// </summary>
public class GoToDefinitionTests(ITestOutputHelper output) : VSCodeIntegrationTestBase(output)
{
    [Fact]
    public async Task GoToDefinition_ComponentReference_NavigatesToComponent()
    {
        // Arrange - Open Counter.razor from the dotnet new blazor template
        // Counter.razor has @onclick="IncrementCount" which we can use for Go to Definition
        await OpenFileAsync("Components/Pages/Counter.razor");

        // Navigate to the IncrementCount method reference in @onclick
        // Line 11 in the template: <button class="btn btn-primary" @onclick="IncrementCount">Click me</button>
        await Editor.GoToLineAsync(11);

        // Find and select "IncrementCount" - use Ctrl+F to search
        await Editor.FindTextAsync("IncrementCount");
        await Page.Keyboard.PressAsync("Escape"); // Close find dialog

        // Act - Go to Definition should navigate to the method in the @code block
        var navigated = await Razor.VerifyGoToDefinitionAsync("Counter.razor");

        // Assert - should stay in Counter.razor (method is defined in same file)
        Assert.True(navigated, "Expected Go to Definition to work on IncrementCount method");
    }

    [Fact]
    public async Task GoToDefinition_CSharpSymbol_NavigatesToDefinition()
    {
        // Arrange - Open Counter.razor
        await OpenFileAsync("Components/Pages/Counter.razor");

        // Navigate to currentCount usage in the IncrementCount method
        // Line 17: "        currentCount++;"
        // Use GoToWordAsync to find the first occurrence and position on it
        await Editor.GoToWordAsync("currentCount++");

        // Act
        await Editor.GoToDefinitionAsync();

        // Assert - should stay in the same file (currentCount is defined in same file)
        var currentFile = await Editor.GetCurrentFileNameAsync();
        Assert.NotNull(currentFile);
        Assert.Contains("Counter.razor", currentFile, StringComparison.OrdinalIgnoreCase);
    }
}
