// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.E2ETests.Infrastructure;

/// <summary>
/// Helper methods for interacting with VS Code's editor in Playwright tests.
/// Provides high-level abstractions for common editor operations.
/// </summary>
public class VSCodeEditor(IPage page, TestSettings settings, ITestOutputHelper? output = null)
{
    private readonly IPage _page = page;
    private readonly TestSettings _settings = settings;
    private readonly ITestOutputHelper? _output = output;

    // Default polling interval for condition-based waits
    private const int DefaultPollIntervalMs = 100;

    private void Log(string message)
    {
        _output?.WriteLine(message);
    }

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
    private static bool ContainsIgnoringWhitespace(string text, string substring)
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
    /// <returns>The value that satisfied the condition.</returns>
    /// <exception cref="TimeoutException">Thrown if the condition is not met within the timeout.</exception>
    public static async Task<T> WaitForConditionAsync<T>(
        Func<Task<T>> getValue,
        Func<T, bool> condition,
        TimeSpan timeout,
        int initialDelayMs = DefaultPollIntervalMs)
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

        throw new TimeoutException($"Condition not met within {timeout.TotalSeconds} seconds");
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
    /// Gets the active editor's text content.
    /// </summary>
    public async Task<string> GetEditorTextAsync()
    {
        // VS Code uses Monaco editor with view lines
        var lines = await _page.QuerySelectorAllAsync(".view-line");
        var textParts = new List<string>();

        foreach (var line in lines)
        {
            var text = await line.TextContentAsync();
            if (text != null)
            {
                textParts.Add(text);
            }
        }

        return string.Join("\n", textParts);
    }

    /// <summary>
    /// Waits for the VS Code quick input widget (command palette, go to line, etc.) to appear.
    /// </summary>
    /// <param name="timeoutMs">Timeout in milliseconds.</param>
    public async Task WaitForQuickInputAsync(int timeoutMs = 5000)
    {
        await _page.WaitForSelectorAsync(".quick-input-widget", new PageWaitForSelectorOptions
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
            await _page.WaitForSelectorAsync(".quick-input-widget", new PageWaitForSelectorOptions
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
    /// Types text at the current cursor position.
    /// </summary>
    public async Task TypeAsync(string text, int delayMs = 50)
    {
        await _page.Keyboard.TypeAsync(text, new KeyboardTypeOptions { Delay = delayMs });
    }

    /// <summary>
    /// Presses a key or key combination.
    /// </summary>
    public async Task PressAsync(string key)
    {
        await _page.Keyboard.PressAsync(key);
    }

    /// <summary>
    /// Triggers IntelliSense completion and waits for it to appear.
    /// </summary>
    /// <param name="waitForList">If true, waits for the completion list to appear.</param>
    /// <param name="timeout">Timeout for waiting. Uses LspTimeout if not specified.</param>
    public async Task<bool> TriggerCompletionAsync(bool waitForList = true, TimeSpan? timeout = null)
    {
        await _page.Keyboard.PressAsync("Control+Space");

        if (waitForList)
        {
            return await WaitForCompletionListAsync(timeout);
        }

        return true;
    }

    /// <summary>
    /// Waits for the completion list to appear.
    /// </summary>
    public async Task<bool> WaitForCompletionListAsync(TimeSpan? timeout = null)
    {
        timeout ??= _settings.LspTimeout;

        try
        {
            // Try multiple possible selectors for the suggest widget (VS Code versions vary)
            var selectors = new[]
            {
                ".suggest-widget.visible",
                ".monaco-list.suggest-widget",
                ".editor-widget.suggest-widget",
                "[widgetid='editor.widget.suggestWidget']"
            };

            foreach (var selector in selectors)
            {
                try
                {
                    await _page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = (float)(timeout.Value.TotalMilliseconds / selectors.Length)
                    });
                    return true;
                }
                catch (TimeoutException)
                {
                    // Try next selector
                }
            }

            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the items in the completion list.
    /// </summary>
    public async Task<List<string>> GetCompletionItemsAsync()
    {
        var items = await _page.QuerySelectorAllAsync(".monaco-list-row .suggest-widget .monaco-icon-label-container");
        var results = new List<string>();

        foreach (var item in items)
        {
            var text = await item.TextContentAsync();
            if (!string.IsNullOrEmpty(text))
            {
                results.Add(text);
            }
        }

        return results;
    }

    /// <summary>
    /// Accepts the currently selected completion item.
    /// </summary>
    public async Task AcceptCompletionAsync()
    {
        await _page.Keyboard.PressAsync("Enter");
        // Wait briefly for the completion to be inserted - use a short poll
        await Task.Delay(100);
    }

    /// <summary>
    /// Triggers hover information at the current cursor position and waits for it to appear.
    /// </summary>
    /// <param name="waitForHover">If true, waits for the hover content to appear.</param>
    /// <param name="timeout">Timeout for waiting. Uses LspTimeout if not specified.</param>
    public async Task<bool> TriggerHoverAsync(bool waitForHover = true, TimeSpan? timeout = null)
    {
        // Move mouse to the current cursor position and hover
        var cursor = await _page.QuerySelectorAsync(".cursor");
        if (cursor == null)
        {
            return false;
        }

        var box = await cursor.BoundingBoxAsync();
        if (box == null)
        {
            return false;
        }

        await _page.Mouse.MoveAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2));

        if (waitForHover)
        {
            return await WaitForHoverAsync(timeout);
        }

        return true;
    }

    /// <summary>
    /// Waits for hover content to appear.
    /// </summary>
    public async Task<bool> WaitForHoverAsync(TimeSpan? timeout = null)
    {
        timeout ??= _settings.LspTimeout;

        try
        {
            await _page.WaitForSelectorAsync(".monaco-hover-content", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = (float)timeout.Value.TotalMilliseconds
            });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the hover content text.
    /// </summary>
    public async Task<string?> GetHoverContentAsync()
    {
        var hover = await _page.QuerySelectorAsync(".monaco-hover-content");
        return hover != null ? await hover.TextContentAsync() : null;
    }

    /// <summary>
    /// Triggers Go to Definition and waits for navigation.
    /// </summary>
    /// <param name="expectedFileName">If provided, waits until a file containing this name is opened.</param>
    /// <param name="timeout">Timeout for waiting.</param>
    public async Task GoToDefinitionAsync(string? expectedFileName = null, TimeSpan? timeout = null)
    {
        timeout ??= _settings.LspTimeout;
        var originalFile = await GetCurrentFileNameAsync();

        await _page.Keyboard.PressAsync("F12");

        if (expectedFileName != null)
        {
            // Wait for navigation to the expected file
            await WaitForConditionAsync(
                GetCurrentFileNameAsync,
                fileName => fileName?.Contains(expectedFileName, StringComparison.OrdinalIgnoreCase) == true,
                timeout.Value);
        }
        else
        {
            // Wait for any navigation (file name changes or peek definition appears)
            await WaitForConditionAsync(
                async () =>
                {
                    var currentFile = await GetCurrentFileNameAsync();
                    var peekVisible = await _page.QuerySelectorAsync(".peekview-widget") != null;
                    return currentFile != originalFile || peekVisible;
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
        timeout ??= _settings.LspTimeout;
        var originalFile = await GetCurrentFileNameAsync();

        // Get the cursor position
        var cursor = await _page.QuerySelectorAsync(".cursor") ?? throw new InvalidOperationException("Cannot find cursor position");
        var box = await cursor.BoundingBoxAsync() ?? throw new InvalidOperationException("Cannot get cursor bounding box");

        // Ctrl+Click at the cursor position
        await _page.Keyboard.DownAsync("Control");
        await _page.Mouse.ClickAsync(box.X + (box.Width / 2), box.Y + (box.Height / 2));
        await _page.Keyboard.UpAsync("Control");

        if (expectedFileName != null)
        {
            // Wait for navigation to the expected file
            await WaitForConditionAsync(
                GetCurrentFileNameAsync,
                fileName => fileName?.Contains(expectedFileName, StringComparison.OrdinalIgnoreCase) == true,
                timeout.Value);
        }
        else
        {
            // Wait for any navigation (file name changes or peek definition appears)
            await WaitForConditionAsync(
                async () =>
                {
                    var currentFile = await GetCurrentFileNameAsync();
                    var peekVisible = await _page.QuerySelectorAsync(".peekview-widget") != null;
                    return currentFile != originalFile || peekVisible;
                },
                timeout.Value);
        }
    }

    /// <summary>
    /// Checks if there are any error diagnostics visible.
    /// </summary>
    public async Task<bool> HasErrorDiagnosticsAsync()
    {
        var errorSquiggles = await _page.QuerySelectorAllAsync(".squiggly-error");
        return errorSquiggles.Count > 0;
    }

    /// <summary>
    /// Checks if there are any warning diagnostics visible.
    /// </summary>
    public async Task<bool> HasWarningDiagnosticsAsync()
    {
        var warningSquiggles = await _page.QuerySelectorAllAsync(".squiggly-warning");
        return warningSquiggles.Count > 0;
    }

    /// <summary>
    /// Waits for diagnostics to appear or disappear using smart polling.
    /// </summary>
    public async Task WaitForDiagnosticsAsync(bool expectErrors = true, TimeSpan? timeout = null)
    {
        timeout ??= _settings.LspTimeout;

        await WaitForConditionAsync(
            HasErrorDiagnosticsAsync,
            hasErrors => hasErrors == expectErrors,
            timeout.Value);
    }

    /// <summary>
    /// Opens the Quick Fix menu (Ctrl+.) and waits for code actions to appear.
    /// </summary>
    /// <param name="waitForActions">If true, waits for the code actions menu to appear.</param>
    /// <param name="timeout">Timeout for waiting.</param>
    public async Task<bool> OpenQuickFixMenuAsync(bool waitForActions = true, TimeSpan? timeout = null)
    {
        await _page.Keyboard.PressAsync("Control+.");

        if (waitForActions)
        {
            return await WaitForCodeActionsAsync(timeout);
        }

        return true;
    }

    /// <summary>
    /// Waits for the code actions menu to appear.
    /// </summary>
    public async Task<bool> WaitForCodeActionsAsync(TimeSpan? timeout = null)
    {
        timeout ??= _settings.LspTimeout;

        try
        {
            await _page.WaitForSelectorAsync(".context-view.monaco-menu-container", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = (float)timeout.Value.TotalMilliseconds
            });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Formats the document (Shift+Alt+F) and waits for formatting to complete.
    /// </summary>
    /// <param name="timeout">Timeout for waiting.</param>
    public async Task FormatDocumentAsync(TimeSpan? timeout = null)
    {
        timeout ??= _settings.LspTimeout;
        var beforeFormat = await GetEditorTextAsync();

        await _page.Keyboard.PressAsync("Shift+Alt+f");

        // Wait for either the text to change, or a reasonable time for no-op formatting
        try
        {
            await WaitForConditionAsync(
                GetEditorTextAsync,
                afterFormat => afterFormat != beforeFormat,
                TimeSpan.FromSeconds(2));
        }
        catch (TimeoutException)
        {
            // Formatting may have been a no-op (already formatted)
            // This is acceptable
        }
    }

    /// <summary>
    /// Gets the current file name from the tab.
    /// </summary>
    public async Task<string?> GetCurrentFileNameAsync()
    {
        var activeTab = await _page.QuerySelectorAsync(".tab.active .monaco-icon-label-container");
        return activeTab != null ? await activeTab.TextContentAsync() : null;
    }

    /// <summary>
    /// Moves the cursor to a specific line and column.
    /// </summary>
    public async Task GoToLineAsync(int line, int column = 1)
    {
        // Ctrl+G opens Go to Line dialog
        await _page.Keyboard.PressAsync("Control+g");
        await WaitForQuickInputAsync();

        await _page.Keyboard.TypeAsync($"{line}:{column}");
        await _page.Keyboard.PressAsync("Enter");

        await WaitForQuickInputToCloseAsync();
    }

    /// <summary>
    /// Selects all text in the editor.
    /// </summary>
    public async Task SelectAllAsync()
    {
        await _page.Keyboard.PressAsync("Control+a");
        // Selection is synchronous, minimal wait
        await Task.Delay(50);
    }

    /// <summary>
    /// Saves the current file and waits for the save to complete.
    /// </summary>
    public async Task SaveAsync()
    {
        await _page.Keyboard.PressAsync("Control+s");

        // Wait for the "dirty" indicator to disappear from the tab
        try
        {
            await WaitForConditionAsync(
                async () =>
                {
                    var dirtyIndicator = await _page.QuerySelectorAsync(".tab.active.dirty");
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
    /// Undoes the last action.
    /// </summary>
    public async Task UndoAsync()
    {
        await _page.Keyboard.PressAsync("Control+z");
        // Undo is synchronous, minimal wait
        await Task.Delay(50);
    }

    /// <summary>
    /// Opens the command palette and waits for it to appear.
    /// </summary>
    public async Task OpenCommandPaletteAsync()
    {
        await _page.Keyboard.PressAsync("Control+Shift+p");
        await WaitForQuickInputAsync();
    }

    /// <summary>
    /// Executes a command via the command palette.
    /// </summary>
    public async Task ExecuteCommandAsync(string command)
    {
        await OpenCommandPaletteAsync();
        await _page.Keyboard.TypeAsync(command);

        // Wait for the command to appear in the list
        await WaitForConditionAsync(
            async () =>
            {
                var items = await _page.QuerySelectorAllAsync(".quick-input-list .monaco-list-row");
                return items.Count > 0;
            },
            TimeSpan.FromSeconds(5));

        await _page.Keyboard.PressAsync("Enter");
        await WaitForQuickInputToCloseAsync();
    }

    /// <summary>
    /// Opens the Find and Replace dialog and waits for it to appear.
    /// </summary>
    public async Task OpenFindReplaceAsync()
    {
        await _page.Keyboard.PressAsync("Control+h");

        // Wait for the find widget to appear
        await _page.WaitForSelectorAsync(".editor-widget.find-widget", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });
    }

    /// <summary>
    /// Opens the Find dialog and searches for text.
    /// </summary>
    public async Task FindTextAsync(string text)
    {
        await _page.Keyboard.PressAsync("Control+f");

        // Wait for the find widget to appear
        await _page.WaitForSelectorAsync(".editor-widget.find-widget", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });

        await _page.Keyboard.TypeAsync(text);
        await _page.Keyboard.PressAsync("Enter"); // Find next

        // Brief wait for highlight to appear
        await Task.Delay(100);
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
        await _page.Keyboard.PressAsync("Control+f");

        // Wait for the find widget to appear
        await _page.WaitForSelectorAsync(".editor-widget.find-widget", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 5000
        });

        await _page.Keyboard.TypeAsync(word);
        await _page.Keyboard.PressAsync("Enter"); // Find and select first occurrence

        // Close the find dialog
        await _page.Keyboard.PressAsync("Escape");

        // Wait for find widget to close
        try
        {
            await _page.WaitForSelectorAsync(".editor-widget.find-widget.visible", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Hidden,
                Timeout = 2000
            });
        }
        catch (TimeoutException)
        {
            // Widget may already be hidden
        }

        if (selectWord)
        {
            // Use Ctrl+D to select the word at cursor
            await _page.Keyboard.PressAsync("Control+d");
        }
    }

    /// <summary>
    /// Triggers rename symbol (F2) and waits for the rename input to appear.
    /// </summary>
    public async Task RenameSymbolAsync(string newName, TimeSpan? timeout = null)
    {
        timeout ??= _settings.LspTimeout;

        await _page.Keyboard.PressAsync("F2");

        // Wait for the rename input box to appear
        await _page.WaitForSelectorAsync(".rename-box", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = (float)timeout.Value.TotalMilliseconds
        });

        await _page.Keyboard.TypeAsync(newName);
        await _page.Keyboard.PressAsync("Enter");

        // Wait for the rename to complete (rename box disappears)
        await _page.WaitForSelectorAsync(".rename-box", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Hidden,
            Timeout = (float)timeout.Value.TotalMilliseconds
        });
    }

    /// <summary>
    /// Triggers Find All References (Shift+F12) and waits for references panel.
    /// </summary>
    public async Task FindAllReferencesAsync(TimeSpan? timeout = null)
    {
        timeout ??= _settings.LspTimeout;

        await _page.Keyboard.PressAsync("Shift+F12");

        // Wait for the references panel or peek view to appear
        await WaitForConditionAsync(
            async () =>
            {
                var peekView = await _page.QuerySelectorAsync(".peekview-widget");
                var referencesPanel = await _page.QuerySelectorAsync("[id='workbench.panel.referencesView']");
                return peekView != null || referencesPanel != null;
            },
            timeout.Value);
    }

    /// <summary>
    /// Opens the Output panel.
    /// </summary>
    public async Task OpenOutputPanelAsync()
    {
        Log("Opening Output panel...");

        // Use command palette to open Output panel
        await ExecuteCommandAsync("View: Show Output");

        // Wait for the panel to be visible - it should have the .part.panel class
        try
        {
            await _page.WaitForSelectorAsync(".part.panel", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5000
            });
            Log("Output panel visible (.part.panel)");

            // Also wait for a monaco editor to appear in the panel
            await _page.WaitForSelectorAsync(".part.panel .monaco-editor", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 3000
            });
            Log("Monaco editor visible in panel");
        }
        catch (TimeoutException ex)
        {
            Log($"Warning: Could not confirm output panel is visible: {ex.Message}");
        }
    }

    /// <summary>
    /// Selects a specific output channel by name.
    /// </summary>
    /// <param name="channelName">The name of the output channel (e.g., "Razor Log").</param>
    public async Task SelectOutputChannelAsync(string channelName)
    {
        Log($"Selecting output channel: {channelName}");

        // Try using the command palette first - this is more reliable
        await OpenCommandPaletteAsync();
        await _page.Keyboard.TypeAsync($"Output: Show Output Channel");

        // Wait for the command to appear
        await WaitForConditionAsync(
            async () =>
            {
                var items = await _page.QuerySelectorAllAsync(".quick-input-list .monaco-list-row");
                return items.Count > 0;
            },
            TimeSpan.FromSeconds(3));

        await _page.Keyboard.PressAsync("Enter");

        // Now type the channel name in the second quick input
        await WaitForQuickInputAsync();
        await _page.Keyboard.TypeAsync(channelName);
        await Task.Delay(300);
        await _page.Keyboard.PressAsync("Enter");
        await WaitForQuickInputToCloseAsync();

        Log($"Selected output channel: {channelName}");

        // Give the output panel time to update
        await Task.Delay(500);
    }

    /// <summary>
    /// Gets the text content of the currently visible output panel.
    /// </summary>
    public async Task<string> GetOutputContentAsync()
    {
        // VS Code output panel uses a Monaco editor which virtualizes content.
        // Lines may be wrapped, so we join with spaces instead of newlines to handle wrapped text.

        // First, try to get content via JavaScript evaluation
        var jsResult = await _page.EvaluateAsync<string>(@"
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
            Log($"Got output content via JS ({jsResult.Length} chars)");
            Log($"Output content: {jsResult}");
            return jsResult;
        }

        Log("JS evaluation returned empty, trying DOM selectors...");

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
            var container = await _page.QuerySelectorAsync(selector);
            if (container != null)
            {
                var text = await container.TextContentAsync();
                Log($"Selector '{selector}' found, text length: {text?.Length ?? 0}");
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }
            else
            {
                Log($"Selector '{selector}' not found");
            }
        }

        // Debug: dump what we can find in the panel
        var debugInfo = await _page.EvaluateAsync<string>(@"
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
        Log($"Debug: {debugInfo}");

        return string.Empty;
    }

    /// <summary>
    /// Checks if the output panel contains specific text, ignoring whitespace differences.
    /// </summary>
    /// <param name="text">The text to search for.</param>
    public async Task<bool> OutputContainsAsync(string text)
    {
        var content = await GetOutputContentAsync();
        var contains = ContainsIgnoringWhitespace(content, text);
        Log($"Output contains '{text}': {contains}");
        return contains;
    }

    /// <summary>
    /// Waits for specific text to appear in an output channel.
    /// </summary>
    /// <param name="channelName">The output channel name.</param>
    /// <param name="expectedText">The text to wait for.</param>
    /// <param name="timeout">Timeout for waiting.</param>
    public async Task WaitForOutputTextAsync(string channelName, string expectedText, TimeSpan? timeout = null)
    {
        timeout ??= _settings.LspTimeout;

        Log($"Waiting for '{expectedText}' in output channel '{channelName}'...");

        await OpenOutputPanelAsync();
        await SelectOutputChannelAsync(channelName);

        await WaitForConditionAsync(
            async () => await OutputContainsAsync(expectedText),
            timeout.Value);

        Log($"Found '{expectedText}' in output channel '{channelName}'");
    }
}
