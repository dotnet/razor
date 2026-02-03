// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Playwright;

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests.Services;

/// <summary>
/// Services for navigation (Go to Definition, Find References) operations in integration tests.
/// </summary>
public class NavigationServices(IntegrationTestServices testServices)
{

    /// <summary>
    /// Triggers Go to Definition and waits for navigation.
    /// </summary>
    /// <param name="expectedFileName">If provided, waits until a file containing this name is opened.</param>
    /// <param name="timeout">Timeout for waiting.</param>
    public async Task GoToDefinitionAsync(string? expectedFileName = null, TimeSpan? timeout = null)
    {
        timeout ??= testServices.Settings.LspTimeout;
        var originalFile = await testServices.Editor.GetCurrentFileNameAsync();
        var originalPosition = await testServices.Editor.GetCursorPositionAsync();

        await testServices.Input.PressAsync("F12");

        if (expectedFileName != null)
        {
            // Wait for navigation to the expected file
            await EditorService.WaitForConditionAsync(
                testServices.Editor.GetCurrentFileNameAsync,
                fileName => fileName?.Contains(expectedFileName, StringComparison.OrdinalIgnoreCase) == true,
                timeout.Value);
        }
        else
        {
            // Wait for any navigation: file name changes, cursor position changes, or peek definition appears
            await EditorService.WaitForConditionAsync(
                async () =>
                {
                    var currentFile = await testServices.Editor.GetCurrentFileNameAsync();
                    var currentPosition = await testServices.Editor.GetCursorPositionAsync();
                    var peekVisible = await testServices.Playwright.Page.Locator(".peekview-widget").CountAsync() > 0;

                    // Check if file changed, position changed, or peek appeared
                    var fileChanged = currentFile != originalFile;
                    var positionChanged = originalPosition != null && currentPosition != null &&
                        (originalPosition.Value.Line != currentPosition.Value.Line ||
                         originalPosition.Value.Column != currentPosition.Value.Column);

                    return fileChanged || positionChanged || peekVisible;
                },
                timeout.Value);
        }
    }

    /// <summary>
    /// Triggers Go to Definition with Ctrl+Click on the current cursor position.
    /// </summary>
    /// <param name="expectedFileName">If provided, waits until a file containing this name is opened.</param>
    /// <param name="timeout">Timeout for waiting.</param>
    public async Task CtrlClickGoToDefinitionAsync(string? expectedFileName = null, TimeSpan? timeout = null)
    {
        timeout ??= testServices.Settings.LspTimeout;
        var originalFile = await testServices.Editor.GetCurrentFileNameAsync();

        // Get the cursor position (use First since there may be multiple cursor elements)
        var cursorLocator = testServices.Playwright.Page.Locator(".cursor").First;
        if (await cursorLocator.CountAsync() == 0)
        {
            throw new InvalidOperationException("Cannot find cursor position");
        }
        var box = await cursorLocator.BoundingBoxAsync() ?? throw new InvalidOperationException("Cannot get cursor bounding box");

        // Ctrl+Click (Cmd+Click on macOS) at the cursor position
        await testServices.Input.ClickWithPrimaryModifierAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2));

        if (expectedFileName != null)
        {
            // Wait for navigation to the expected file
            await EditorService.WaitForConditionAsync(
                testServices.Editor.GetCurrentFileNameAsync,
                fileName => fileName?.Contains(expectedFileName, StringComparison.OrdinalIgnoreCase) == true,
                timeout.Value);
        }
        else
        {
            // Wait for any navigation (file name changes or peek definition appears)
            await EditorService.WaitForConditionAsync(
                async () =>
                {
                    var currentFile = await testServices.Editor.GetCurrentFileNameAsync();
                    var peekVisible = await testServices.Playwright.Page.Locator(".peekview-widget").CountAsync() > 0;
                    return currentFile != originalFile || peekVisible;
                },
                timeout.Value);
        }
    }

    /// <summary>
    /// Triggers Find All References (Shift+F12) and waits for references panel.
    /// </summary>
    public async Task FindAllReferencesAsync(TimeSpan? timeout = null)
    {
        timeout ??= testServices.Settings.LspTimeout;

        await testServices.Input.PressAsync("Shift+F12");

        // Wait for the references panel or peek view to appear
        await EditorService.WaitForConditionAsync(
            async () =>
            {
                var peekViewCount = await testServices.Playwright.Page.Locator(".peekview-widget").CountAsync();
                var referencesPanelCount = await testServices.Playwright.Page.Locator("[id='workbench.panel.referencesView']").CountAsync();
                return peekViewCount > 0 || referencesPanelCount > 0;
            },
            timeout.Value);
    }
}
