// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests.Services;

/// <summary>
/// Services for keyboard and mouse input in integration tests.
/// </summary>
public class InputService(IntegrationTestServices testServices) : ServiceBase(testServices)
{
    // Platform-specific primary modifier key (Cmd on macOS, Ctrl elsewhere)
    private static readonly string s_primaryModifier = OperatingSystem.IsMacOS() ? "Meta" : "Control";

    /// <summary>
    /// Types text at the current cursor position.
    /// </summary>
    public async Task TypeAsync(string text, int delayMs = 50)
    {
        await TestServices.Playwright.Page.Keyboard.TypeAsync(text, new Microsoft.Playwright.KeyboardTypeOptions { Delay = delayMs });
    }

    /// <summary>
    /// Presses a key or key combination.
    /// </summary>
    public async Task PressAsync(string key)
    {
        await TestServices.Playwright.Page.Keyboard.PressAsync(key);
    }

    /// <summary>
    /// Presses a key with the platform-appropriate primary modifier (Cmd on macOS, Ctrl on Windows/Linux).
    /// </summary>
    /// <param name="key">The key to press with the modifier (e.g., "s" for save, "ArrowLeft" for word navigation)</param>
    public async Task PressWithPrimaryModifierAsync(string key)
    {
        await TestServices.Playwright.Page.Keyboard.PressAsync($"{s_primaryModifier}+{key}");
    }

    /// <summary>
    /// Presses a key with Shift and the platform-appropriate primary modifier.
    /// </summary>
    /// <param name="key">The key to press with Shift+modifier</param>
    public async Task PressWithShiftPrimaryModifierAsync(string key)
    {
        await TestServices.Playwright.Page.Keyboard.PressAsync($"{s_primaryModifier}+Shift+{key}");
    }

    /// <summary>
    /// Presses a key with Control, regardless of the operating system.
    /// Use this for VS Code shortcuts that use Control even on macOS (e.g., Ctrl+G for "Go to Line").
    /// </summary>
    /// <param name="key">The key to press with Control</param>
    public async Task PressWithControlAsync(string key)
    {
        await TestServices.Playwright.Page.Keyboard.PressAsync($"Control+{key}");
    }

    /// <summary>
    /// Navigates to the end of the current line using the appropriate key for each platform.
    /// On Windows/Linux: End key. On macOS: Cmd+Right Arrow.
    /// </summary>
    public async Task PressEndOfLineAsync()
    {
        if (OperatingSystem.IsMacOS())
        {
            await TestServices.Playwright.Page.Keyboard.PressAsync("Meta+ArrowRight");
        }
        else
        {
            await TestServices.Playwright.Page.Keyboard.PressAsync("End");
        }
    }
}
