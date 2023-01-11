// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests;

public class WrapWithTagTests : AbstractRazorEditorTest
{
    [IdeFact]
    public async Task WrapWithTag_RootLevelElement()
    {
        // Open the file
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.CounterRazorFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("h1", charsOffset: -1, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);

        // Act
        TestServices.Input.Send("{ALT}{SHIFT}w");

        // Assert
        await TestServices.Editor.WaitForCurrentLineTextAsync("<div><h1>Counter</h1></div>", ControlledHangMitigatingCancellationToken);
    }

    [IdeFact]
    public async Task WrapWithTag_ChildElement()
    {
        // Open the file
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.FetchDataRazorFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("em", charsOffset: -1, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);

        // Act
        TestServices.Input.Send("{ALT}{SHIFT}w");

        // Assert
        await TestServices.Editor.WaitForCurrentLineTextAsync("<p><div><em>Loading...</em></div></p>", ControlledHangMitigatingCancellationToken);
    }

    [IdeFact]
    public async Task WrapWithTag_SelfClosingTag()
    {
        // Open the file
        await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.IndexRazorFile, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.PlaceCaretAsync("SurveyPrompt", charsOffset: -1, ControlledHangMitigatingCancellationToken);

        await TestServices.Editor.WaitForComponentClassificationAsync(ControlledHangMitigatingCancellationToken);

        // Act
        TestServices.Input.Send("{ALT}{SHIFT}w");

        // Assert
        await TestServices.Editor.WaitForCurrentLineTextAsync("<div><SurveyPrompt Title=\"How is Blazor working for you?\" /></div>", ControlledHangMitigatingCancellationToken);
    }
}
