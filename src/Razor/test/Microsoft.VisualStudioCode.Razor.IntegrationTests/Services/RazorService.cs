// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests.Services;

/// <summary>
/// Services for Razor language server operations in integration tests.
/// </summary>
public class RazorService(IntegrationTestServices testServices)
{
    /// <summary>
    /// Waits for the Razor language server to be fully initialized by verifying semantic tokenization is working.
    /// This checks that Razor component tags are being colorized (shows "razorComponentElement" in token inspector).
    /// </summary>
    public async Task WaitForReadyAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(60);
        var deadline = DateTime.UtcNow + timeout.Value;
        var attempt = 0;

        testServices.Logger.Log("Waiting for Razor language server to be ready (checking semantic tokens)...");

        while (DateTime.UtcNow < deadline)
        {
            attempt++;
            testServices.Logger.Log($"Razor ready check attempt {attempt}...");

            try
            {
                // Open Home.razor which contains <PageTitle> component
                await testServices.Editor.OpenFileAsync("Components/Pages/Home.razor");

                // Wait a moment for the file to be processed
                await Task.Delay(500);

                // Navigate to PageTitle - it's typically on line 3
                // Home.razor usually has: @page "/" then <PageTitle>Home</PageTitle>
                await testServices.Editor.GoToWordAsync("PageTitle", selectWord: false);

                // Run "Developer: Inspect Editor Tokens and Scopes" command
                await testServices.Editor.ExecuteCommandAsync("Developer: Inspect Editor Tokens and Scopes");

                // Wait for the token inspector popup to appear
                await Task.Delay(500);

                // Check if the popup contains "razorComponentElement"
                var hasRazorToken = await CheckForRazorTokenAsync();

                // Close the token inspector by pressing Escape
                await testServices.Input.PressAsync("Escape");
                await Task.Delay(100);

                // Close the file
                await testServices.Input.PressWithPrimaryModifierAsync("w");
                await Task.Delay(200);

                if (hasRazorToken)
                {
                    testServices.Logger.Log($"Razor language server is ready - semantic tokens verified (attempt {attempt})");
                    return;
                }

                testServices.Logger.Log($"Razor tokens not yet available (attempt {attempt}), retrying...");

                // Wait before next attempt
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                testServices.Logger.Log($"Razor ready check attempt {attempt} failed: {ex.Message}");

                // Try to close any open file/dialog before retrying
                try
                {
                    await testServices.Input.PressAsync("Escape");
                    await testServices.Input.PressWithPrimaryModifierAsync("w");
                }
                catch
                {
                    // Ignore cleanup errors
                }

                await Task.Delay(1000);
            }
        }

        throw new TimeoutException($"Razor language server did not become ready within {timeout.Value.TotalSeconds} seconds");
    }

    /// <summary>
    /// Checks if the token inspector popup contains "razorComponentElement".
    /// </summary>
    private async Task<bool> CheckForRazorTokenAsync()
    {
        // The token inspector shows in a hover-like widget
        // Look for the content that contains token scope information
        var tokenContent = await testServices.Playwright.Page.EvaluateAsync<string?>(@"
            (() => {
                // The token inspector typically uses a hover widget
                // Try multiple selectors to find it
                const selectors = [
                    '.monaco-hover',
                    '.monaco-hover-content', 
                    '.editor-hover-content',
                    '.hover-row',
                    '.hover-contents',
                    '[class*=""hover""]'
                ];
                
                for (const selector of selectors) {
                    const elements = document.querySelectorAll(selector);
                    for (const el of elements) {
                        const text = el.textContent || '';
                        // Look for razorComponentElement anywhere in the text
                        if (text.includes('razorComponentElement')) {
                            return text;
                        }
                    }
                }
                
                // Fallback: search all visible elements for razorComponentElement
                const allElements = document.querySelectorAll('*');
                for (const el of allElements) {
                    const text = el.textContent || '';
                    if (text.includes('razorComponentElement') && text.length < 5000) {
                        return text.substring(0, 1000);
                    }
                }
                
                return null;
            })()
        ");

        if (string.IsNullOrEmpty(tokenContent))
        {
            testServices.Logger.Log("Token inspector content not found or no razorComponentElement");
            return false;
        }

        testServices.Logger.Log($"Found razorComponentElement in token inspector");
        return true;
    }
}
