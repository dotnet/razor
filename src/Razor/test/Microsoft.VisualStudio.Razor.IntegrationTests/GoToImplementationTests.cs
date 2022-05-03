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
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, CounterRazorFile, HangMitigatingCancellationToken);

            await TestServices.Editor.PlaceCaretAsync("IncrementCount", charsOffset: -1, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeGoToImplementationAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.WaitForCurrentLineTextAsync("private void IncrementCount()", HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task GoToImplementation_CSharpClass()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, IndexRazorFile, HangMitigatingCancellationToken);

            // Change text to refer back to Program class
            await TestServices.Editor.SetTextAsync(@"<SurveyPrompt Title=""@nameof(Program)", HangMitigatingCancellationToken);
            await TestServices.Editor.PlaceCaretAsync("Program", charsOffset: -1, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeGoToImplementationAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.WaitForActiveWindowAsync("Program.cs", HangMitigatingCancellationToken);
        }
    }
}
