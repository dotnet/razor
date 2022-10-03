// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    public class GoToImplementationTests : AbstractRazorEditorTest
    {
        [IdeFact]
        public async Task GoToImplementation_SameFile()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.CounterRazorFile, ControlledHangMitigatingCancellationToken);

            await TestServices.Editor.PlaceCaretAsync("IncrementCount", charsOffset: -1, ControlledHangMitigatingCancellationToken);

            // Act (Ctrl+12 == GoToImplementation
            TestServices.Input.Send("^{F12}");

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

            // Act (Ctrl+12 == GoToImplementation
            TestServices.Input.Send("^{F12}");

            // Assert
            await TestServices.Editor.WaitForActiveWindowAsync("Program.cs", ControlledHangMitigatingCancellationToken);
        }
    }
}
