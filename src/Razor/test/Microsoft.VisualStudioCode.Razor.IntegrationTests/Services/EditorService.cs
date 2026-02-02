// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests.Services;

/// <summary>
/// Helper methods for interacting with VS Code's editor in Playwright tests.
/// Provides high-level abstractions for common editor operations.
/// </summary>
public class EditorService(IntegrationTestServices testServices)
{
    // Default polling interval for condition-based waits
    private const int DefaultPollIntervalMs = 100;

    // Track the currently open file's relative path
    private string? _currentOpenFile;

    /// <summary>
    /// Checks if <paramref name="text"/> contains <paramref name="substring"/> while ignoring whitespace in both strings.
    /// </summary>
    /// <remarks>
    /// When extracting text from the VS Code DOM, whitespace can be unpredictable:
    /// <list type="bullet">
    ///   <item>Monaco editor uses non-breaking spaces (U+00A0) instead of regular spaces</item>
    ///   <item>Text may wrap across multiple DOM elements, causing line breaks mid-word</item>
    ///   <item>Joining wrapped lines introduces extra spaces at wrap points</item>
    ///   <item>Various Unicode whitespace characters may appear (figure space, narrow no-break space, etc.)</item>
    /// </list>
    /// This method efficiently scans through both strings, skipping whitespace characters,
    /// and attempts to match without allocating new strings.
    /// </remarks>
    public static bool ContainsIgnoringWhitespace(string text, string substring)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(substring))
        {
            return false;
        }

        var textIndex = 0;

        while (textIndex < text.Length)
        {
            // Try to match starting from this position
            if (MatchesAtPositionIgnoringWhitespace(text, textIndex, substring))
            {
                return true;
            }

            textIndex++;
        }

        return false;
    }

    /// <summary>
    /// Checks if the substring matches starting at the given position in text, ignoring whitespace in both.
    /// </summary>
    private static bool MatchesAtPositionIgnoringWhitespace(string text, int startIndex, string substring)
    {
        var textIndex = startIndex;
        var subIndex = 0;

        while (subIndex < substring.Length)
        {
            // Skip whitespace in the substring
            if (IsWhitespaceOrSpecial(substring[subIndex]))
            {
                subIndex++;
                continue;
            }

            // Skip whitespace in the input text
            while (textIndex < text.Length && IsWhitespaceOrSpecial(text[textIndex]))
            {
                textIndex++;
            }

            // If we've run out of text, no match
            if (textIndex >= text.Length)
            {
                return false;
            }

            // Compare characters (case-insensitive)
            if (char.ToLowerInvariant(text[textIndex]) != char.ToLowerInvariant(substring[subIndex]))
            {
                return false;
            }

            textIndex++;
            subIndex++;
        }

        return true;
    }

    /// <summary>
    /// Checks if a character is whitespace or a special zero-width/formatting character.
    /// </summary>
    private static bool IsWhitespaceOrSpecial(char c)
    {
        return char.IsWhiteSpace(c)
            || c == '\u2060'  // Word joiner (zero-width)
            || c == '\u200B'  // Zero-width space
            || c == '\uFEFF'; // Byte order mark (zero-width no-break space)
    }

    /// <summary>
    /// Polls for a condition to become true, with exponential backoff.
    /// This replaces arbitrary Task.Delay() calls with smart condition-based waiting.
    /// </summary>
    /// <typeparam name="T">The type of value to retrieve and check.</typeparam>
    /// <param name="getValue">Function to retrieve the current value.</param>
    /// <param name="condition">Predicate that returns true when the condition is met.</param>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="initialDelayMs">Initial delay between polls (will increase with backoff).</param>
    /// <param name="callerName">The name of the calling method (automatically populated).</param>
    /// <returns>The value that satisfied the condition.</returns>
    /// <exception cref="TimeoutException">Thrown if the condition is not met within the timeout.</exception>
    public static async Task<T> WaitForConditionAsync<T>(
        Func<Task<T>> getValue,
        Func<T, bool> condition,
        TimeSpan timeout,
        int initialDelayMs = DefaultPollIntervalMs,
        [CallerMemberName] string? callerName = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        var delayMs = initialDelayMs;
        var maxDelayMs = 1000; // Cap backoff at 1 second

        while (DateTime.UtcNow < deadline)
        {
            var value = await getValue();
            if (condition(value))
            {
                return value;
            }

            await Task.Delay(delayMs);
            delayMs = Math.Min(delayMs * 2, maxDelayMs); // Exponential backoff
        }

        throw new TimeoutException($"Condition {callerName} not met within {timeout.TotalSeconds} seconds");
    }

    /// <summary>
    /// Polls for a condition to become true, with exponential backoff.
    /// Overload for simple boolean conditions.
    /// </summary>
    public static async Task WaitForConditionAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        int initialDelayMs = DefaultPollIntervalMs)
    {
        await WaitForConditionAsync(condition, result => result, timeout, initialDelayMs);
    }

    /// <summary>
    /// Gets the active editor's text content by saving the file and reading from disk.
    /// Waits for the file contents to change from their on-disk state to ensure VS Code has flushed updates.
    /// Use this when you expect the editor buffer to differ from what's currently on disk.
    /// </summary>
    public async Task<string> WaitForEditorTextChangeAsync()
    {
        if (_currentOpenFile == null)
        {
            testServices.Logger.Log("WaitForEditorTextChangeAsync: No file currently open");
            return string.Empty;
        }

        var filePath = Path.Combine(testServices.Workspace.Path, _currentOpenFile);

        // Read the original file contents before saving
        var originalContents = "";
        try
        {
            originalContents = await ReadFileExclusiveAsync(filePath);
            testServices.Logger.Log($"WaitForEditorTextChangeAsync: BEFORE contents ({originalContents.Length} chars):\n{originalContents}");
        }
        catch (IOException ex)
        {
            testServices.Logger.Log($"WaitForEditorTextChangeAsync: Failed to read original file: {ex.Message}");
        }

        // Trigger save
        await SaveAsync();

        // Wait for the file contents to change, indicating VS Code has flushed to disk
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        var delayMs = 50;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var currentContents = await ReadFileExclusiveAsync(filePath);
                if (currentContents != originalContents)
                {
                    // File has been updated, return the new contents
                    testServices.Logger.Log($"WaitForEditorTextChangeAsync: AFTER contents ({currentContents.Length} chars):\n{currentContents}");
                    return currentContents;
                }
            }
            catch (IOException)
            {
                // File might be locked by VS Code, continue waiting
            }

            await Task.Delay(delayMs);
            delayMs = Math.Min(delayMs * 2, 500); // Exponential backoff, cap at 500ms
        }

        // Timeout: return the last read contents (file may not have changed)
        testServices.Logger.Log($"WaitForEditorTextChangeAsync: Timeout waiting for contents change, returning current contents");
        try
        {
            var text = await ReadFileExclusiveAsync(filePath);
            testServices.Logger.Log($"WaitForEditorTextChangeAsync: AFTER contents (fallback, {text.Length} chars):\n{text}");
            return text;
        }
        catch (IOException ex)
        {
            testServices.Logger.Log($"WaitForEditorTextChangeAsync: Failed to read file: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Reads a file using ReadWrite access to ensure VS Code has finished writing.
    /// Opening with ReadWrite will fail if another process has the file open for writing.
    /// Retries for a few seconds if the file is locked.
    /// </summary>
    private static async Task<string> ReadFileExclusiveAsync(string filePath)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
        var delayMs = 50;

        while (true)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                // File is locked, wait and retry
                await Task.Delay(delayMs);
                delayMs = Math.Min(delayMs * 2, 500);
            }
        }
    }

    /// <summary>
    /// Waits for the VS Code quick input widget (command palette, go to line, etc.) to appear.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    public async Task WaitForQuickInputAsync(int timeoutMs = 5000)
    {
        // Wait for the input field itself which is more reliable than the container
        await testServices.Playwright.Page.WaitForSelectorAsync(".quick-input-widget .quick-input-box input", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });
    }

    /// <summary>
    /// Waits for the VS Code quick input widget to close.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    public async Task WaitForQuickInputToCloseAsync(int timeoutMs = 2000)
    {
        try
        {
            await testServices.Playwright.Page.WaitForSelectorAsync(".quick-input-widget", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = timeoutMs
            });
        }
        catch (TimeoutException)
        {
            // Widget may already be hidden
        }
    }

    /// <summary>
    /// Gets the current file name from the tab.
    /// </summary>
    public async Task<string?> GetCurrentFileNameAsync()
    {
        var activeTab = await testServices.Playwright.Page.QuerySelectorAsync(".tab.active .monaco-icon-label-container");
        return activeTab != null ? await activeTab.TextContentAsync() : null;
    }

    /// <summary>
    /// Gets the current cursor position (line and column) from the VS Code status bar.
    /// </summary>
    /// <returns>A tuple of (line, column) or null if position cannot be determined.</returns>
    public async Task<(int Line, int Column)?> GetCursorPositionAsync()
    {
        // VS Code shows cursor position in the status bar as "Ln X, Col Y"
        // The element has class "editor-status-selection" or similar
        var statusText = await testServices.Playwright.Page.EvaluateAsync<string?>(@"
            (() => {
                // Try multiple selectors for the cursor position in status bar
                const selectors = [
                    '.editor-status-selection',
                    '[aria-label*=""Go to Line""]',
                    '.statusbar-item a[aria-label*=""Ln""]',
                    '.statusbar-item:has-text(""Ln"")'
                ];
                
                for (const selector of selectors) {
                    const el = document.querySelector(selector);
                    if (el && el.textContent) {
                        return el.textContent;
                    }
                }
                
                // Fallback: search all status bar items
                const items = document.querySelectorAll('.statusbar-item');
                for (const item of items) {
                    const text = item.textContent || '';
                    if (text.includes('Ln') && text.includes('Col')) {
                        return text;
                    }
                }
                
                return null;
            })()
        ");

        if (string.IsNullOrEmpty(statusText))
        {
            return null;
        }

        // Parse "Ln X, Col Y" format
        // Example: "Ln 13, Col 17" or "Ln 13, Col 17 (5 selected)"
        var match = System.Text.RegularExpressions.Regex.Match(
            statusText,
            @"Ln\s*(\d+),?\s*Col\s*(\d+)");

        if (match.Success &&
            int.TryParse(match.Groups[1].Value, out var line) &&
            int.TryParse(match.Groups[2].Value, out var column))
        {
            return (line, column);
        }

        return null;
    }

    /// <summary>
    /// Moves the cursor to a specific line and column.
    /// </summary>
    public async Task GoToLineAsync(int line, int column = 1)
    {
        // Ctrl+G opens Go to Line dialog (Control on all platforms, including macOS)
        await testServices.Input.PressWithControlAsync("g");
        await WaitForQuickInputAsync();

        await testServices.Input.TypeAsync($"{line}:{column}");
        await testServices.Input.PressAsync("Enter");

        await WaitForQuickInputToCloseAsync();

        // Wait for the cursor position to actually update in the status bar.
        // The status bar update can lag behind the actual cursor movement.
        await WaitForConditionAsync(
            GetCursorPositionAsync,
            pos => pos?.Line == line,
            TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Selects all text in the editor.
    /// </summary>
    public async Task SelectAllAsync()
    {
        await testServices.Input.PressWithPrimaryModifierAsync("a");
        // Selection is synchronous, minimal wait
        await Task.Delay(50);
    }

    /// <summary>
    /// Saves the current file and waits for the save to complete.
    /// </summary>
    public async Task SaveAsync()
    {
        testServices.Logger.Log("Saving document.");
        await testServices.Input.PressWithPrimaryModifierAsync("s");

        // Wait for the "dirty" indicator to disappear from the tab
        try
        {
            await WaitForConditionAsync(
                async () =>
                {
                    var dirtyIndicator = await testServices.Playwright.Page.QuerySelectorAsync(".tab.active.dirty");
                    testServices.Logger.Log("Dirty indicator: " + (dirtyIndicator != null ? "present" : "not present"));
                    return dirtyIndicator == null;
                },
                TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            // File may not have been dirty, or indicator differs
        }

    }

    /// <summary>
    /// Waits for the editor to have unsaved changes (dirty indicator on tab).
    /// </summary>
    public async Task WaitForEditorDirtyAsync()
    {
        await WaitForConditionAsync(
            async () =>
            {
                var dirtyIndicator = await testServices.Playwright.Page.QuerySelectorAsync(".tab.active.dirty");
                testServices.Logger.Log("Dirty indicator: " + (dirtyIndicator != null ? "present" : "not present"));
                return dirtyIndicator != null;
            },
            TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Undoes the last action.
    /// </summary>
    public async Task UndoAsync()
    {
        await testServices.Input.PressWithPrimaryModifierAsync("z");
        // Undo is synchronous, minimal wait
        await Task.Delay(50);
    }

    /// <summary>
    /// Opens the command palette and waits for it to appear.
    /// </summary>
    public async Task OpenCommandPaletteAsync()
    {
        await testServices.Input.PressWithShiftPrimaryModifierAsync("p");
        await WaitForQuickInputAsync();
    }

    /// <summary>
    /// Executes a command via the command palette.
    /// </summary>
    public async Task ExecuteCommandAsync(string command)
    {
        await OpenCommandPaletteAsync();
        await testServices.Input.TypeAsync(command);

        // Wait for the command to appear in the list
        await WaitForConditionAsync(
            async () =>
            {
                var items = await testServices.Playwright.Page.QuerySelectorAllAsync(".quick-input-list .monaco-list-row");
                return items.Count > 0;
            },
            TimeSpan.FromSeconds(5));

        await testServices.Input.PressAsync("Enter");
        await WaitForQuickInputToCloseAsync();
    }

    /// <summary>
    /// Opens the Find and Replace dialog and waits for it to appear.
    /// </summary>
    public async Task OpenFindReplaceAsync()
    {
        await testServices.Input.PressWithPrimaryModifierAsync("h");

        // Wait for the find widget to appear
        await testServices.Playwright.Page.WaitForSelectorAsync(".editor-widget.find-widget", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });
    }

    /// <summary>
    /// Opens the Find dialog, searches for text, and closes the dialog.
    /// </summary>
    public async Task FindTextAsync(string text)
    {
        await testServices.Input.PressWithPrimaryModifierAsync("f");

        // Wait for the find widget to appear
        await testServices.Playwright.Page.WaitForSelectorAsync(".editor-widget.find-widget", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });

        await testServices.Input.TypeAsync(text);
        await testServices.Input.PressAsync("Enter"); // Find next

        // Brief wait for highlight to appear
        await Task.Delay(100);

        // Close the find dialog
        await testServices.Input.PressAsync("Escape");
    }

    /// <summary>
    /// Navigates to a specific word/symbol in the file and positions the cursor on it.
    /// Uses Find to locate the word, then closes the find dialog and selects the word.
    /// </summary>
    /// <param name="word">The word to navigate to.</param>
    /// <param name="selectWord">If true, selects the entire word after navigating.</param>
    public async Task GoToWordAsync(string word, bool selectWord = false)
    {
        // Use Find to navigate to the word
        await testServices.Input.PressWithPrimaryModifierAsync("f");

        // Wait for the find widget to appear
        await testServices.Playwright.Page.WaitForSelectorAsync(".editor-widget.find-widget", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });

        await testServices.Input.TypeAsync(word);

        await Task.Delay(100);

        // Close the find dialog - press Escape twice:
        // First Escape unfocuses the find input, second closes the widget
        await testServices.Input.PressAsync("Escape");
        await Task.Delay(50);
        await testServices.Input.PressAsync("Escape");

        // Wait for find widget to close
        try
        {
            await testServices.Playwright.Page.WaitForSelectorAsync(".editor-widget.find-widget.visible", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = 2000
            });
        }
        catch (TimeoutException)
        {
            // Widget still visible - try one more escape and take screenshot for debugging
            await testServices.Input.PressAsync("Escape");
            await Task.Delay(100);
            await testServices.Playwright.TakeScreenshotAsync($"GoToWord_{word}_StillVisible");
        }

        if (!selectWord)
        {
            // GoToWord leaves cursor at end, but if not selecting, move to start of word
            await testServices.Input.PressAsync("ArrowLeft");
        }
    }

    /// <summary>
    /// Opens a file in the editor and waits for it to be active.
    /// </summary>
    public async Task OpenFileAsync(string relativePath)
    {
        testServices.Logger.Log($"Opening file: {relativePath}");

        // Use the Quick Open dialog (Ctrl+P / Cmd+P) to open the file
        await testServices.Input.PressWithPrimaryModifierAsync("p");

        // Wait for Quick Open input to appear - wait for the input field itself which is more reliable
        await testServices.Playwright.Page.WaitForSelectorAsync(".quick-input-widget .quick-input-box input", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });

        await testServices.Input.TypeAsync(relativePath);

        // Wait briefly for the file list to populate
        await Task.Delay(200);

        await testServices.Input.PressAsync("Enter");

        // Wait for the file to be open by checking the active tab
        var expectedFileName = Path.GetFileName(relativePath);
        await WaitForConditionAsync(
            async () =>
            {
                var activeTab = await testServices.Playwright.Page.QuerySelectorAsync(".tab.active .monaco-icon-label-container");
                if (activeTab == null)
                    return false;
                var tabText = await activeTab.TextContentAsync();
                return tabText?.Contains(expectedFileName, StringComparison.OrdinalIgnoreCase) == true;
            },
            testServices.Settings.LspTimeout);

        // Track the currently open file for GetEditorTextAsync
        _currentOpenFile = relativePath;

        testServices.Logger.Log($"File opened: {relativePath}");
    }
}
