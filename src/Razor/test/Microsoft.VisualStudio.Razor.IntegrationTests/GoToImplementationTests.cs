﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.IntegrationTests;

public class GoToImplementationTests(ITestOutputHelper testOutputHelper) : AbstractRazorEditorTest(testOutputHelper)
{
    [IdeFact]
    public async Task GoToImplementation_SameFile()
    {
        // Open the file
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.CounterRazorFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("IncrementCount", charsOffset: -1, ControlledHangMitigatingCancellationToken);

        // Act
        await TestServices.Editor.InvokeGoToImplementationAsync(ControlledHangMitigatingCancellationToken);

        // Assert
        await TestServices.Editor.WaitForCurrentLineTextAsync("private void IncrementCount()", ControlledHangMitigatingCancellationToken);
    }

    [IdeFact]
    public async Task GoToImplementation_CSharpClass()
    {
        // Open the file
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.IndexRazorFile, ControlledHangMitigatingCancellationToken);

        // Change text to refer back to Program class
        await TestServices.Editor.SetTextAsync(@"<SurveyPrompt Title=""@nameof(Program)", ControlledHangMitigatingCancellationToken);
        await TestServices.Editor.PlaceCaretAsync("Program", charsOffset: -1, ControlledHangMitigatingCancellationToken);

        // Act
        await TestServices.Editor.InvokeGoToImplementationAsync(ControlledHangMitigatingCancellationToken);

        // Assert
        await TestServices.Editor.WaitForActiveWindowAsync("Program.cs", ControlledHangMitigatingCancellationToken);
    }
}
