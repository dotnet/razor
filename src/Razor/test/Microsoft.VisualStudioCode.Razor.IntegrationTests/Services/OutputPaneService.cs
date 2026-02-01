// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Playwright;

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests.Services;

/// <summary>
/// Services for interacting with VS Code's Output pane in integration tests.
/// </summary>
public class OutputPaneService(IntegrationTestServices testServices)
{
    /// <summary>
    /// Opens the Output panel.
    /// </summary>
    public async Task OpenAsync()
    {
        testServices.Logger.Log("Opening Output panel...");

        // Use command palette to open Output panel
        await testServices.Editor.ExecuteCommandAsync("View: Show Output");

        // Wait for the panel to be visible - it should have the .part.panel class
        try
        {
            await testServices.Playwright.Page.WaitForSelectorAsync(".part.panel", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5000
            });
            testServices.Logger.Log("Output panel visible (.part.panel)");

            // Also wait for a monaco editor to appear in the panel
            await testServices.Playwright.Page.WaitForSelectorAsync(".part.panel .monaco-editor", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 3000
            });
            testServices.Logger.Log("Monaco editor visible in panel");
        }
        catch (TimeoutException ex)
        {
            testServices.Logger.Log($"Warning: Could not confirm output panel is visible: {ex.Message}");
        }
    }

    /// <summary>
    /// Selects a specific output channel by name.
    /// </summary>
    /// <param name="channelName">The name of the output channel (e.g., "Razor Log").</param>
    public async Task SelectChannelAsync(string channelName)
    {
        testServices.Logger.Log($"Selecting output channel: {channelName}");

        // Try using the command palette first - this is more reliable
        await testServices.Editor.OpenCommandPaletteAsync();
        await testServices.Input.TypeAsync($"Output: Show Output Channel");

        // Wait for the command to appear
        await EditorService.WaitForConditionAsync(
            async () =>
            {
                var items = await testServices.Playwright.Page.QuerySelectorAllAsync(".quick-input-list .monaco-list-row");
                return items.Count > 0;
            },
            TimeSpan.FromSeconds(3));

        await testServices.Input.PressAsync("Enter");

        // Now type the channel name in the second quick input
        await testServices.Editor.WaitForQuickInputAsync();
        await testServices.Input.TypeAsync(channelName);
        await Task.Delay(300);
        await testServices.Input.PressAsync("Enter");
        await testServices.Editor.WaitForQuickInputToCloseAsync();

        testServices.Logger.Log($"Selected output channel: {channelName}");

        // Give the output panel time to update
        await Task.Delay(500);
    }

    /// <summary>
    /// Gets the text content of the currently visible output panel.
    /// </summary>
    public async Task<string> GetContentAsync()
    {
        // VS Code output panel uses a Monaco editor which virtualizes content.
        // Lines may be wrapped, so we join with spaces instead of newlines to handle wrapped text.

        // First, try to get content via JavaScript evaluation
        var jsResult = await testServices.Playwright.Page.EvaluateAsync<string>(@"
            (() => {
                // Try to find the output panel's editor
                const panels = document.querySelectorAll('.part.panel .monaco-editor');
                for (const panel of panels) {
                    // Get all visible lines
                    const lines = panel.querySelectorAll('.view-line');
                    if (lines.length > 0) {
                        const texts = [];
                        for (const line of lines) {
                            texts.push(line.textContent || '');
                        }
                        // Join with space to handle wrapped lines
                        return texts.join(' ');
                    }
                }

                // Fallback: look for any view-lines in the panel area
                const bottomPanel = document.querySelector('.part.panel');
                if (bottomPanel) {
                    const viewLines = bottomPanel.querySelectorAll('.view-line');
                    if (viewLines.length > 0) {
                        const texts = [];
                        for (const line of viewLines) {
                            texts.push(line.textContent || '');
                        }
                        return texts.join(' ');
                    }
                }

                return '';
            })()
        ");

        if (!string.IsNullOrEmpty(jsResult))
        {
            testServices.Logger.Log($"Got output content via JS ({jsResult.Length} chars)");
            testServices.Logger.Log($"Output content: {jsResult}");
            return jsResult;
        }

        testServices.Logger.Log("JS evaluation returned empty, trying DOM selectors...");

        // Fallback: try multiple DOM selectors
        var selectors = new[]
        {
            ".part.panel .monaco-editor .view-lines",
            ".part.panel .view-lines",
            ".panel .output .view-lines",
            "[id='workbench.panel.output'] .view-lines"
        };

        foreach (var selector in selectors)
        {
            var container = await testServices.Playwright.Page.QuerySelectorAsync(selector);
            if (container != null)
            {
                var text = await container.TextContentAsync();
                testServices.Logger.Log($"Selector '{selector}' found, text length: {text?.Length ?? 0}");
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
            else
            {
                testServices.Logger.Log($"Selector '{selector}' not found");
            }
        }

        // Debug: dump what we can find in the panel
        var debugInfo = await testServices.Playwright.Page.EvaluateAsync<string>(@"
            (() => {
                const panel = document.querySelector('.part.panel');
                if (!panel) return 'No panel found';

                const editors = panel.querySelectorAll('.monaco-editor');
                let info = `Panel found. ${editors.length} monaco-editors. `;

                for (let i = 0; i < editors.length; i++) {
                    const ed = editors[i];
                    const lines = ed.querySelectorAll('.view-line');
                    info += `Editor ${i}: ${lines.length} lines. `;
                    if (lines.length > 0) {
                        info += `First line: '${lines[0].textContent?.substring(0, 50)}...' `;
                    }
                }

                return info;
            })()
        ");
        testServices.Logger.Log($"Debug: {debugInfo}");

        return string.Empty;
    }

    /// <summary>
    /// Checks if the output panel contains specific text, ignoring whitespace differences.
    /// </summary>
    /// <param name="text">The text to search for.</param>
    public async Task<bool> ContainsAsync(string text)
    {
        var content = await GetContentAsync();
        var contains = EditorService.ContainsIgnoringWhitespace(content, text);
        testServices.Logger.Log($"Output contains '{text}': {contains}");
        return contains;
    }

    /// <summary>
    /// Waits for specific text to appear in an output channel.
    /// </summary>
    /// <param name="channelName">The output channel name.</param>
    /// <param name="expectedText">The text to wait for.</param>
    /// <param name="timeout">Timeout for waiting.</param>
    public async Task WaitForTextAsync(string channelName, string expectedText, TimeSpan? timeout = null)
    {
        timeout ??= testServices.Settings.LspTimeout;

        testServices.Logger.Log($"Waiting for '{expectedText}' in output channel '{channelName}'...");

        await OpenAsync();
        await SelectChannelAsync(channelName);

        await EditorService.WaitForConditionAsync(
            async () => await ContainsAsync(expectedText),
            timeout.Value);

        testServices.Logger.Log($"Found '{expectedText}' in output channel '{channelName}'");
    }
}
