// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudioCode.Razor.E2ETests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.E2ETests.Scenarios;

/// <summary>
/// E2E tests for diagnostics (error squiggles) in Razor files.
/// </summary>
public class DiagnosticsTests(ITestOutputHelper output) : VSCodeIntegrationTestBase(output)
{
    [Fact]
    public async Task Diagnostics_CSharpSyntaxError_ShowsErrorSquiggle()
    {
        // Arrange
        await OpenFileAsync("BlazorApp/Components/Pages/Home.razor");

        // Act & Assert
        var diagnosticsAppeared = await Razor.VerifyDiagnosticsAppearAsync();
        Assert.True(diagnosticsAppeared, "Expected error diagnostics to appear for syntax error");
    }

    [Fact]
    public async Task Diagnostics_FixError_RemovesSquiggle()
    {
        // Arrange
        await OpenFileAsync("BlazorApp/Components/Pages/Home.razor");

        // Act & Assert
        var diagnosticsDisappeared = await Razor.VerifyDiagnosticsDisappearAsync();
        Assert.True(diagnosticsDisappeared, "Expected error diagnostics to disappear after fix");
    }

    [Fact]
    public async Task Diagnostics_ValidFile_NoErrors()
    {
        // Arrange
        await OpenFileAsync("BlazorApp/Components/Counter.razor");

        // Wait for diagnostics to settle (expect no errors)
        try
        {
            await Editor.WaitForDiagnosticsAsync(expectErrors: false, timeout: TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException)
        {
            // If we timeout waiting for "no errors", that's still useful info
        }

        // Act
        var hasErrors = await Editor.HasErrorDiagnosticsAsync();

        // Assert
        Assert.False(hasErrors, "Valid Razor file should have no error diagnostics");
    }
}
