// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudioCode.Razor.E2ETests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.E2ETests.Scenarios;

/// <summary>
/// E2E tests for rename symbol in Razor files.
/// </summary>
public class RenameTests(ITestOutputHelper output) : VSCodeIntegrationTestBase(output)
{
    [Fact]
    public async Task Rename_LocalVariable_RenamesAllOccurrences()
    {
        // Arrange
        await OpenFileAndWaitForReadyAsync("BlazorApp/Components/Pages/Home.razor");

        // Navigate to the 'message' variable
        await Editor.GoToLineAsync(19); // private string message = "Test message";

        // Select the variable name
        await Editor.PressAsync("Control+d"); // Select word

        // Act
        await Editor.RenameSymbolAsync("newMessage");

        // Assert
        var text = await Editor.GetEditorTextAsync();
        Assert.Contains("newMessage", text);

        // Undo to restore original state
        await Editor.UndoAsync();
        await Editor.SaveAsync();
    }

    [Fact]
    public async Task Rename_ComponentParameter_UpdatesUsages()
    {
        // Arrange
        await OpenFileAndWaitForReadyAsync("BlazorApp/Components/Counter.razor");

        // Navigate to the IncrementAmount parameter
        await Editor.GoToLineAsync(16); // public int IncrementAmount { get; set; }

        // Select the parameter name
        await Editor.PressAsync("Control+d");

        // Act
        await Editor.RenameSymbolAsync("StepAmount");

        // Assert
        var text = await Editor.GetEditorTextAsync();
        Assert.Contains("StepAmount", text);

        // Undo to restore original state
        await Editor.UndoAsync();
        await Editor.SaveAsync();
    }
}
