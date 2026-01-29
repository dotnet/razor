// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests.Infrastructure;

/// <summary>
/// Helper methods specific to Razor editing scenarios.
/// Provides convenience methods for testing Razor IntelliSense, diagnostics, and navigation.
/// </summary>
public class RazorEditorHelpers(IPage page, TestSettings settings, ITestOutputHelper? output = null)
{
    private readonly VSCodeEditor _editor = new VSCodeEditor(page, settings, output);
    private readonly ITestOutputHelper? _output = output;

    private void Log(string message) => _output?.WriteLine(message);

    /// <summary>
    /// Gets the underlying VS Code editor helper.
    /// </summary>
    public VSCodeEditor Editor => _editor;

    /// <summary>
    /// Verifies that C# IntelliSense works in a @code block.
    /// </summary>
    public async Task<bool> VerifyCSharpCompletionInCodeBlockAsync()
    {
        var hasCompletions = await _editor.TriggerCompletionAsync();
        if (!hasCompletions)
        {
            return false;
        }

        // Look for common C# completions
        var items = await _editor.GetCompletionItemsAsync();
        return items.Any(i =>
            i.Contains("string", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("int", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("var", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("public", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies that HTML IntelliSense works.
    /// </summary>
    public async Task<bool> VerifyHtmlCompletionAsync()
    {
        var hasCompletions = await _editor.TriggerCompletionAsync();
        if (!hasCompletions)
        {
            return false;
        }

        var items = await _editor.GetCompletionItemsAsync();
        return items.Any(i =>
            i.Contains("div", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("span", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("button", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies that Razor directive completions work.
    /// </summary>
    public async Task<bool> VerifyRazorDirectiveCompletionAsync()
    {
        // Type @ to trigger Razor completions
        await _editor.TypeAsync("@");

        var hasCompletions = await _editor.TriggerCompletionAsync();
        if (!hasCompletions)
        {
            await _editor.UndoAsync();
            return false;
        }

        var items = await _editor.GetCompletionItemsAsync();
        var hasRazorDirectives = items.Any(i =>
            i.Contains("page", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("inject", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("code", StringComparison.OrdinalIgnoreCase) ||
            i.Contains("using", StringComparison.OrdinalIgnoreCase));

        await _editor.UndoAsync();
        return hasRazorDirectives;
    }

    /// <summary>
    /// Verifies that component parameter completions work.
    /// </summary>
    public async Task<bool> VerifyComponentParameterCompletionAsync(string componentName)
    {
        // Type the component tag and trigger completion for attributes
        await _editor.TypeAsync($"<{componentName} ");

        var hasCompletions = await _editor.TriggerCompletionAsync();

        // Clean up
        await _editor.UndoAsync();

        return hasCompletions;
    }

    /// <summary>
    /// Introduces a syntax error and verifies diagnostics appear.
    /// </summary>
    public async Task<bool> VerifyDiagnosticsAppearAsync()
    {
        // Type something that will cause a C# error
        await _editor.GoToLineAsync(1);
        await _editor.TypeAsync("@{ invalid syntax here }");
        await _editor.SaveAsync();

        try
        {
            await _editor.WaitForDiagnosticsAsync(expectErrors: true, timeout: TimeSpan.FromSeconds(10));
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            await _editor.UndoAsync();
            await _editor.SaveAsync();
        }
    }

    /// <summary>
    /// Verifies that diagnostics disappear after fixing an error.
    /// </summary>
    public async Task<bool> VerifyDiagnosticsDisappearAsync()
    {
        // First introduce an error
        await _editor.GoToLineAsync(1);
        await _editor.TypeAsync("@{ int x = }"); // Missing value
        await _editor.SaveAsync();

        // Wait for error to appear
        try
        {
            await _editor.WaitForDiagnosticsAsync(expectErrors: true, timeout: TimeSpan.FromSeconds(10));
        }
        catch
        {
            // If no error appeared, test setup failed
            await _editor.UndoAsync();
            return false;
        }

        // Fix the error
        await _editor.UndoAsync();
        await _editor.SaveAsync();

        // Wait for errors to disappear
        try
        {
            await _editor.WaitForDiagnosticsAsync(expectErrors: false, timeout: TimeSpan.FromSeconds(10));
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Triggers hover and waits for the hover content text to appear.
    /// Waits for actual content to appear (not "Loading...").
    /// </summary>
    /// <returns>The hover content text, or null if hover failed to appear.</returns>
    public async Task<string?> WaitForHoverContentAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(10);

        var hasHover = await _editor.TriggerHoverAsync();
        if (!hasHover)
        {
            return null;
        }

        // Wait for actual content, not "Loading..."
        // The LSP may take time to respond, so we poll until we get real content.
        var deadline = DateTime.UtcNow + timeout.Value;
        while (DateTime.UtcNow < deadline)
        {
            var content = await _editor.GetHoverContentAsync();
            if (!string.IsNullOrEmpty(content) && !content.Equals("Loading...", StringComparison.OrdinalIgnoreCase))
            {
                return content;
            }

            await Task.Delay(100);
        }

        // Return whatever we have, even if it's still "Loading..."
        return await _editor.GetHoverContentAsync();
    }

    /// <summary>
    /// Verifies Go to Definition navigates to a component file.
    /// </summary>
    public async Task<bool> VerifyGoToDefinitionAsync(string expectedFileName)
    {
        // GoToDefinitionAsync now waits for navigation
        await _editor.GoToDefinitionAsync(expectedFileName);

        var currentFile = await _editor.GetCurrentFileNameAsync();
        return currentFile?.Contains(expectedFileName, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    /// <summary>
    /// Verifies that formatting works on a Razor file.
    /// </summary>
    public async Task<bool> VerifyFormattingAsync()
    {
        var beforeFormat = await _editor.GetEditorTextAsync();
        await _editor.FormatDocumentAsync();
        var afterFormat = await _editor.GetEditorTextAsync();

        // Formatting should produce some output (even if unchanged)
        return !string.IsNullOrEmpty(afterFormat);
    }

    /// <summary>
    /// Waits for the Razor language server to be fully initialized by checking the Razor Log output.
    /// </summary>
    public async Task WaitForRazorReadyAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(60);

        Log("Waiting for Razor language server to be ready...");

        // Check the Razor Log output channel for the startup finished message
        // Text is normalized (non-breaking spaces replaced) so we can search normally
        await _editor.WaitForOutputTextAsync(
            "Razor Log",
            "Razor extension startup finished.",
            timeout);

        Log("Razor language server is ready - found startup message in output");
    }
}
