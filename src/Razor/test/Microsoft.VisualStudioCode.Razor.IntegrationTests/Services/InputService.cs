// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests.Services;

/// <summary>
/// Services for keyboard and mouse input in integration tests.
/// </summary>
public class InputService(IntegrationTestServices testServices)
{
    // Platform-specific primary modifier key (Cmd on macOS, Ctrl elsewhere)
    private static readonly string s_primaryModifier = OperatingSystem.IsMacOS() ? "Meta" : "Control";

    /// <summary>
    /// Types text at the current cursor position.
    /// </summary>
    public async Task TypeAsync(string text, int delayMs = 50)
    {
        await testServices.Playwright.Page.Keyboard.TypeAsync(text, new Microsoft.Playwright.KeyboardTypeOptions { Delay = delayMs });
    }

    /// <summary>
    /// Presses a key or key combination.
    /// </summary>
    public async Task PressAsync(string key)
    {
        await testServices.Playwright.Page.Keyboard.PressAsync(key);
    }

    /// <summary>
    /// Presses a key with the platform-appropriate primary modifier (Cmd on macOS, Ctrl on Windows/Linux).
    /// </summary>
    /// <param name="key">The key to press with the modifier (e.g., "s" for save, "ArrowLeft" for word navigation)</param>
    public async Task PressWithPrimaryModifierAsync(string key)
    {
        await testServices.Playwright.Page.Keyboard.PressAsync($"{s_primaryModifier}+{key}");
    }

    /// <summary>
    /// Presses a key with Shift and the platform-appropriate primary modifier.
    /// </summary>
    /// <param name="key">The key to press with Shift+modifier</param>
    public async Task PressWithShiftPrimaryModifierAsync(string key)
    {
        await testServices.Playwright.Page.Keyboard.PressAsync($"{s_primaryModifier}+Shift+{key}");
    }

    /// <summary>
    /// Presses a key with Control, regardless of the operating system.
    /// Use this for VS Code shortcuts that use Control even on macOS (e.g., Ctrl+G for "Go to Line").
    /// </summary>
    /// <param name="key">The key to press with Control</param>
    public async Task PressWithControlAsync(string key)
    {
        await testServices.Playwright.Page.Keyboard.PressAsync($"Control+{key}");
    }

    /// <summary>
    /// Navigates to the end of the current line using the appropriate key for each platform.
    /// On Windows/Linux: End key. On macOS: Cmd+Right Arrow.
    /// </summary>
    public async Task PressEndOfLineAsync()
    {
        if (OperatingSystem.IsMacOS())
        {
            await testServices.Playwright.Page.Keyboard.PressAsync("Meta+ArrowRight");
        }
        else
        {
            await testServices.Playwright.Page.Keyboard.PressAsync("End");
        }
    }

    /// <summary>
    /// Navigates to the beginning of the current line using the appropriate key for each platform.
    /// On Windows/Linux: Home key. On macOS: Cmd+Left Arrow.
    /// </summary>
    public async Task PressHomeOfLineAsync()
    {
        if (OperatingSystem.IsMacOS())
        {
            await testServices.Playwright.Page.Keyboard.PressAsync("Meta+ArrowLeft");
        }
        else
        {
            await testServices.Playwright.Page.Keyboard.PressAsync("Home");
        }
    }

    /// <summary>
    /// Performs a click while holding the primary modifier key (Ctrl on Windows/Linux, Cmd on macOS).
    /// </summary>
    /// <param name="x">X coordinate for the click.</param>
    /// <param name="y">Y coordinate for the click.</param>
    public async Task ClickWithPrimaryModifierAsync(float x, float y)
    {
        await testServices.Playwright.Page.Keyboard.DownAsync(s_primaryModifier);
        await testServices.Playwright.Page.Mouse.ClickAsync(x, y);
        await testServices.Playwright.Page.Keyboard.UpAsync(s_primaryModifier);
    }
}
