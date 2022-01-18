// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Xunit;

namespace Microsoft.VisualStudio.Razor.Integration.Test
{
    public class GoToDefinitionTests : AbstractRazorEditorTest
    {
        [IdeFact]
        public async Task GoToDefinition_MethodInSameFile()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, CounterRazorFile, HangMitigatingCancellationToken);

            // Wait for classifications to indicate Razor LSP is up and running
            await TestServices.Editor.WaitForClassificationAsync(HangMitigatingCancellationToken);
            await TestServices.Editor.PlaceCaretAsync("IncrementCount", charsOffset: -1, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeGoToDefinitionAsync(HangMitigatingCancellationToken);

            await TestServices.Editor.WaitForCaretMoveAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.VerifyCurrentLineTextAsync("private void IncrementCount()", HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task GoToDefinition_CSharpClass()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, IndexRazorFile, HangMitigatingCancellationToken);

            // Change text to refer back to Program class
            await TestServices.Editor.SetTextAsync(@"<SurveyPrompt Title=""@nameof(Program)", HangMitigatingCancellationToken);
            await TestServices.Editor.PlaceCaretAsync("Program", charsOffset: -1, HangMitigatingCancellationToken);

            // Wait for classifications to indicate Razor LSP is up and running
            await TestServices.Editor.WaitForClassificationAsync(HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeGoToDefinitionAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.WaitForActiveWindowAsync("Program.cs", HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task GoToDefinition_Component()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, IndexRazorFile, HangMitigatingCancellationToken);

            // Wait for classifications to indicate Razor LSP is up and running
            await TestServices.Editor.WaitForClassificationAsync(HangMitigatingCancellationToken);
            await TestServices.Editor.PlaceCaretAsync("SurveyPrompt", charsOffset: -1, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeGoToDefinitionAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.WaitForActiveWindowAsync("SurveyPrompt.razor", HangMitigatingCancellationToken);
        }

        [IdeFact(Skip = "Won't work until 17.1 P3")]
        public async Task GoToDefinition_ComponentAttribute()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, IndexRazorFile, HangMitigatingCancellationToken);

            // Wait for classifications to indicate Razor LSP is up and running
            await TestServices.Editor.WaitForClassificationAsync(HangMitigatingCancellationToken);
            await TestServices.Editor.PlaceCaretAsync("Title=", charsOffset: -1, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeGoToDefinitionAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.WaitForActiveWindowAsync("SurveyPrompt.razor", HangMitigatingCancellationToken);
            await TestServices.Editor.VerifyCurrentLineTextAsync("public string? Title { get; set; }", HangMitigatingCancellationToken);
        }
    }
}
