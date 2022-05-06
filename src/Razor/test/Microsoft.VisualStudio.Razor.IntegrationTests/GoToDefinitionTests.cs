// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    public class GoToDefinitionTests : AbstractRazorEditorTest
    {
        [IdeFact]
        public async Task GoToDefinition_MethodInSameFile()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.CounterRazorFile, HangMitigatingCancellationToken);

            await TestServices.Editor.PlaceCaretAsync("IncrementCount", charsOffset: -1, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeGoToDefinitionAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.WaitForCurrentLineTextAsync("private void IncrementCount()", HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task GoToDefinition_CSharpClass()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.IndexRazorFile, HangMitigatingCancellationToken);

            // Change text to refer back to Program class
            await TestServices.Editor.SetTextAsync(@"<SurveyPrompt Title=""@nameof(Program)", HangMitigatingCancellationToken);
            await TestServices.Editor.PlaceCaretAsync("Program", charsOffset: -1, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeGoToDefinitionAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.WaitForActiveWindowAsync("Program.cs", HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task GoToDefinition_Component()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.IndexRazorFile, HangMitigatingCancellationToken);

            await TestServices.Editor.PlaceCaretAsync("SurveyPrompt", charsOffset: -1, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeGoToDefinitionAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.WaitForActiveWindowAsync("SurveyPrompt.razor", HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task GoToDefinition_ComponentAttribute()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.IndexRazorFile, HangMitigatingCancellationToken);

            await TestServices.Editor.PlaceCaretAsync("Title=", charsOffset: -1, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeGoToDefinitionAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.WaitForActiveWindowAsync("SurveyPrompt.razor", HangMitigatingCancellationToken);
            await TestServices.Editor.WaitForCurrentLineTextAsync("public string? Title { get; set; }", HangMitigatingCancellationToken);
        }
    }
}
