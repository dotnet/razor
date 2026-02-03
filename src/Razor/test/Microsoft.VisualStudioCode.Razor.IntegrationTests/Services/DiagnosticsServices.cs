// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Playwright;

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests.Services;

/// <summary>
/// Services for diagnostics (error squiggles) operations in integration tests.
/// </summary>
public class DiagnosticsServices(IntegrationTestServices testServices)
{
    /// <summary>
    /// Checks if there are any error diagnostics visible (squiggles in the editor).
    /// </summary>
    public async Task<bool> HasErrorsAsync()
    {
        var errorCount = await testServices.Playwright.Page.Locator(".squiggly-error").CountAsync();
        return errorCount > 0;
    }

    /// <summary>
    /// Checks if there are any warning diagnostics visible (squiggles in the editor).
    /// </summary>
    public async Task<bool> HasWarningsAsync()
    {
        var warningCount = await testServices.Playwright.Page.Locator(".squiggly-warning").CountAsync();
        return warningCount > 0;
    }

    /// <summary>
    /// Waits for diagnostics to appear or disappear using smart polling.
    /// </summary>
    public async Task WaitForDiagnosticsAsync(bool expectErrors = true, TimeSpan? timeout = null)
    {
        timeout ??= testServices.Settings.LspTimeout;

        await EditorService.WaitForConditionAsync(
            HasErrorsAsync,
            hasErrors => hasErrors == expectErrors,
            timeout.Value);
    }

    /// <summary>
    /// Opens the Problems panel.
    /// </summary>
    public async Task OpenProblemsPanelAsync()
    {
        await testServices.Editor.ExecuteCommandAsync("View: Toggle Problems");
        
        // Wait for panel to be visible
        await EditorService.WaitForConditionAsync(
            async () =>
            {
                var panelCount = await testServices.Playwright.Page.Locator(".markers-panel, .problems-panel, [aria-label*='Problems']").CountAsync();
                return panelCount > 0;
            },
            TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Gets all problems (errors and warnings) from the Problems panel.
    /// </summary>
    /// <returns>List of problem messages.</returns>
    public async Task<List<string>> GetProblemsAsync()
    {
        var problems = new List<string>();

        // The problems panel shows items in a tree structure with markers
        var problemItems = await testServices.Playwright.Page.EvaluateAsync<string[]>(@"
            (() => {
                const items = [];
                
                // Look for marker rows in the problems panel
                // Each problem is in a .monaco-list-row containing the marker message
                const rows = document.querySelectorAll('.markers-panel .monaco-list-row, [id=""workbench.panel.markers""] .monaco-list-row');
                
                for (const row of rows) {
                    // Get all text content from the row
                    const text = row.textContent || '';
                    if (text.trim()) {
                        items.push(text.trim());
                    }
                }
                
                // If we didn't find any, try the tree items directly
                if (items.length === 0) {
                    const treeItems = document.querySelectorAll('.markers-panel .monaco-tl-row, .panel .markers-panel-container .monaco-list-row');
                    for (const item of treeItems) {
                        const text = item.textContent || '';
                        if (text.trim()) {
                            items.push(text.trim());
                        }
                    }
                }
                
                return items;
            })()
        ") ?? [];

        problems.AddRange(problemItems.Where(p => !string.IsNullOrWhiteSpace(p)));

        testServices.Logger.Log($"Found {problems.Count} problems: {string.Join("; ", problems.Take(5))}");
        return problems;
    }

    /// <summary>
    /// Waits for a specific problem code to appear in the Problems panel.
    /// </summary>
    /// <param name="problemCode">The diagnostic code to wait for (e.g., "CS1002", "RZ9980").</param>
    /// <param name="timeout">Timeout for waiting.</param>
    public async Task WaitForProblemAsync(string problemCode, TimeSpan? timeout = null)
    {
        timeout ??= testServices.Settings.LspTimeout;

        await EditorService.WaitForConditionAsync(
            async () =>
            {
                var problems = await GetProblemsAsync();
                return problems.Any(p => p.Contains(problemCode, StringComparison.OrdinalIgnoreCase));
            },
            timeout.Value);
    }
}
