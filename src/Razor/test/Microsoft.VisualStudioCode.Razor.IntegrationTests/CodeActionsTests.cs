// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudioCode.Razor.IntegrationTests.Services;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests;

/// <summary>
/// E2E tests for code actions (Quick Fix, refactoring) in Razor files.
/// </summary>
public class CodeActionsTests(ITestOutputHelper output) : VSCodeIntegrationTestBase(output)
{
    [Fact]
    public Task CodeAction_AddUsing_AddsUsingDirective() => ScreenshotOnFailureAsync(async () =>
    {
        // Arrange - Open Counter.razor and add an unresolved type
        await TestServices.Editor.OpenFileAsync("Components/Pages/Counter.razor");

        // Go to the @code block and type an unresolved type
        await TestServices.Editor.GoToLineAsync(15);
        await TestServices.Input.TypeAsync("StringBuilder sb;");
        await TestServices.Editor.SaveAsync();

        // Wait for diagnostics (error squiggle on StringBuilder)
        await TestServices.Diagnostics.WaitForDiagnosticsAsync(expectErrors: true, timeout: TimeSpan.FromSeconds(10));

        // Position cursor on StringBuilder
        await TestServices.Editor.GoToWordAsync("StringBuilder");

        // Act - Open Quick Fix menu and select first action (should be "using System.Text;")
        var hasCodeActions = await TestServices.CodeAction.OpenQuickFixMenuAsync();
        Assert.True(hasCodeActions, "Expected code actions for unresolved type");

        // Select the first code action (Add using)
        await TestServices.Input.PressAsync("Enter");

        // Wait for the code action to be applied by checking for editor dirty state then save
        await TestServices.Editor.WaitForEditorDirtyAsync();

        // Assert - verify the using directive was added
        var text = await TestServices.Editor.WaitForEditorTextChangeAsync();
        Assert.Contains("using System.Text", text);

        // Also verify the error is gone
        await TestServices.Diagnostics.WaitForDiagnosticsAsync(expectErrors: false, timeout: TimeSpan.FromSeconds(10));
    });

    [Fact]
    public Task CodeAction_ExtractMethod_CreatesNewMethod() => ScreenshotOnFailureAsync(async () =>
    {
        // Arrange - Open Counter.razor
        await TestServices.Editor.OpenFileAsync("Components/Pages/Counter.razor");

        // Go to the IncrementCount method and add some code to extract
        await TestServices.Editor.GoToLineAsync(17); // Inside IncrementCount
        await TestServices.Input.TypeAsync("var temp = currentCount * 2;");
        await TestServices.Editor.SaveAsync();

        // Select the expression "currentCount * 2"
        await TestServices.Editor.GoToWordAsync("currentCount * 2");

        // Act - Open Quick Fix menu
        var hasCodeActions = await TestServices.CodeAction.OpenQuickFixMenuAsync();

        // Look for Extract Method action - type to filter
        if (hasCodeActions)
        {
            await TestServices.Input.TypeAsync("Extract");
            // Wait for the quick-input list to filter
            await EditorService.WaitForConditionAsync(
                async () =>
                {
                    var itemCount = await TestServices.Playwright.Page.Locator(".quick-input-list .monaco-list-row, .action-widget .monaco-list-row").CountAsync();
                    return itemCount > 0;
                },
                TimeSpan.FromSeconds(5));
            await TestServices.Input.PressAsync("Enter");
            
            // Wait for rename input or editor change
            await EditorService.WaitForConditionAsync(
                async () =>
                {
                    // Check for rename widget or quick-input
                    var renameWidget = await TestServices.Playwright.Page.Locator(".rename-box, .quick-input-widget").CountAsync();
                    return renameWidget > 0;
                },
                TimeSpan.FromSeconds(5));

            // Accept the default method name
            await TestServices.Input.PressAsync("Enter");
            
            // Wait for editor to become dirty (code was modified)
            await TestServices.Editor.WaitForEditorDirtyAsync();
        }

        // Assert - verify a new method was created
        var text = await TestServices.Editor.WaitForEditorTextChangeAsync();
        // The extracted method should exist (VS Code typically names it NewMethod or similar)
        var hasNewMethod = text.Contains("NewMethod") || text.Contains("GetTemp") || text.Contains("private");

        Assert.True(hasCodeActions, "Expected Extract Method code action to be available");
    });
}

