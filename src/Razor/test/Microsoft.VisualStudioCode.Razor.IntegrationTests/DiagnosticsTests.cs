// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudioCode.Razor.IntegrationTests.Services;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.Razor.IntegrationTests;

/// <summary>
/// E2E tests for diagnostics (error squiggles) in Razor files.
/// </summary>
public class DiagnosticsTests(ITestOutputHelper output) : VSCodeIntegrationTestBase(output)
{
    [Fact]
    public Task Diagnostics_CSharpSyntaxError_ShowsInProblemsPanel() => ScreenshotOnFailureAsync(async () =>
    {
        // Arrange
        await TestServices.Editor.OpenFileAsync("Components/Pages/Home.razor");
        await TestServices.Diagnostics.OpenProblemsPanelAsync();

        // Act - type something that will cause a C# error (missing semicolon)
        await TestServices.Editor.SelectAllAsync();
        await TestServices.Input.TypeAsync("@{ int x = 5 }"); // Missing semicolon

        // Assert - wait for CS1002 (semicolon expected) to appear in Problems panel
        await TestServices.Diagnostics.WaitForProblemAsync("CS1002", timeout: TimeSpan.FromSeconds(5));

        var problems = await TestServices.Diagnostics.GetProblemsAsync();
        Assert.Contains(problems, p => p.Contains("CS1002"));
    });

    [Fact]
    public Task Diagnostics_FixError_RemovesFromProblemsPanel() => ScreenshotOnFailureAsync(async () =>
    {
        // Arrange
        await TestServices.Editor.OpenFileAsync("Components/Pages/Home.razor");
        await TestServices.Diagnostics.OpenProblemsPanelAsync();

        // Introduce an error
        await TestServices.Editor.SelectAllAsync();
        await TestServices.Input.TypeAsync("@{ int x = }"); // Missing value - CS1525

        // Wait for error to appear
        await TestServices.Diagnostics.WaitForDiagnosticsAsync(expectErrors: true, timeout: TimeSpan.FromSeconds(5));

        // Act - fix the error by adding the missing value
        await TestServices.Input.PressAsync("Backspace");
        await TestServices.Input.TypeAsync("5; }");

        // Assert - wait for errors to disappear
        await TestServices.Diagnostics.WaitForDiagnosticsAsync(expectErrors: false, timeout: TimeSpan.FromSeconds(5));
        var hasErrors = await TestServices.Diagnostics.HasErrorsAsync();
        Assert.False(hasErrors, "Expected error diagnostics to disappear after fix");
    });

    [Fact]
    public Task Diagnostics_ValidFile_NoErrors() => ScreenshotOnFailureAsync(async () =>
    {
        // Arrange
        await TestServices.Editor.OpenFileAsync("Components/Pages/Counter.razor");
        await TestServices.Diagnostics.OpenProblemsPanelAsync();

        // Wait for diagnostics to settle
        await Task.Delay(1000);

        // Act
        var hasErrors = await TestServices.Diagnostics.HasErrorsAsync();

        // Assert
        Assert.False(hasErrors, "Valid Razor file should have no error diagnostics");
    });

    [Fact]
    public Task Diagnostics_UnclosedTag_ShowsRZ9980() => ScreenshotOnFailureAsync(async () =>
    {
        var fileName = Path.Combine(TestServices.Workspace.Path, "Components/Pages/Home.razor");
        var contents = File.ReadAllText(fileName);
        contents = contents.Replace("</PageTitle>", ""); // Remove closing tag to introduce error
        File.WriteAllText(fileName, contents);

        await TestServices.Editor.OpenFileAsync("Components/Pages/Home.razor");

        await TestServices.Diagnostics.OpenProblemsPanelAsync();

        await TestServices.Diagnostics.WaitForProblemAsync("RZ1034", timeout: TimeSpan.FromSeconds(5));

        var problems = await TestServices.Diagnostics.GetProblemsAsync();
        Assert.Contains(problems, p => p.Contains("RZ1034"));
    });

    [Fact]
    public Task Diagnostics_UnclosedComponentTag_ShowsRZ9980() => ScreenshotOnFailureAsync(async () =>
    {
        // Arrange
        var fileName = Path.Combine(TestServices.Workspace.Path, "Components/Pages/Home.razor");
        var contents = File.ReadAllText(fileName);
        contents = contents.Replace("</h1>", ""); // Remove closing tag to introduce error
        File.WriteAllText(fileName, contents);

        await TestServices.Editor.OpenFileAsync("Components/Pages/Home.razor");

        await TestServices.Diagnostics.OpenProblemsPanelAsync();

        await TestServices.Diagnostics.WaitForProblemAsync("RZ9980", timeout: TimeSpan.FromSeconds(5));

        var problems = await TestServices.Diagnostics.GetProblemsAsync();
        Assert.Contains(problems, p => p.Contains("RZ9980"));
    });
}

