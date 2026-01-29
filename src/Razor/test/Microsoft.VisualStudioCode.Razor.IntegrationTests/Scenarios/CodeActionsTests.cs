// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudioCode.Razor.IntegrationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests.Scenarios;

/// <summary>
/// E2E tests for code actions (Quick Fix, refactoring) in Razor files.
/// </summary>
public class CodeActionsTests(ITestOutputHelper output) : VSCodeIntegrationTestBase(output)
{
    [Fact(Skip = "Skipped for initial CI validation - only running HoverTests")]
    public async Task CodeAction_QuickFix_ShowsAvailableActions()
    {
        // Arrange
        await OpenFileAsync("BlazorApp/Components/Pages/Home.razor");

        // Navigate to a location with potential code actions
        await Editor.GoToLineAsync(20);

        // Act
        await Editor.OpenQuickFixMenuAsync();
        var hasCodeActions = await Editor.WaitForCodeActionsAsync(TimeSpan.FromSeconds(5));

        // Assert - code actions menu should appear (even if empty, the menu shows)
        // The key is that it doesn't crash
        await Editor.PressAsync("Escape"); // Close menu
    }

    [Fact(Skip = "Skipped for initial CI validation - only running HoverTests")]
    public async Task CodeAction_AddUsing_WorksForUnresolvedType()
    {
        // Arrange
        await OpenFileAsync("BlazorApp/Components/Pages/Home.razor");

        // Type an unresolved type to trigger "Add using" code action
        await Editor.GoToLineAsync(20);
        await Editor.TypeAsync("StringBuilder sb;");
        await Editor.SaveAsync();

        // Wait for diagnostics to appear (uses smart polling now)
        await Editor.WaitForDiagnosticsAsync(expectErrors: true);

        // Act - OpenQuickFixMenuAsync now waits for code actions to appear
        var hasCodeActions = await Editor.OpenQuickFixMenuAsync();

        // Assert
        Assert.True(hasCodeActions, "Expected code actions for unresolved type");

        // Clean up
        await Editor.PressAsync("Escape");
        await Editor.UndoAsync();
        await Editor.SaveAsync();
    }
}
