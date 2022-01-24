// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    public class GoToDefinitionTests : AbstractRazorEditorTest
    {
        [IdeFact]
        public async Task GoToDefinition_MethodInSameFile()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, CounterRazorFile, HangMitigatingCancellationToken);

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

            await TestServices.Editor.PlaceCaretAsync("SurveyPrompt", charsOffset: -1, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeGoToDefinitionAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.WaitForActiveWindowAsync("SurveyPrompt.razor", HangMitigatingCancellationToken);
        }

        [IdeFact]
        public async Task GoToDefinition_ComponentAttribute()
        {
            var version = await TestServices.Shell.GetVersionAsync(HangMitigatingCancellationToken);
            if (version < new System.Version(17, 1, 32113, 165))
            {
                // Functionality under test was added in v17 Preview 3 (17.1.32113.165) so this test will
                // file until CI is updated, so we'll skip it.
                return;
            }

            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, IndexRazorFile, HangMitigatingCancellationToken);

            await TestServices.Editor.PlaceCaretAsync("Title=", charsOffset: -1, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeGoToDefinitionAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.WaitForActiveWindowAsync("SurveyPrompt.razor", HangMitigatingCancellationToken);
            await TestServices.Editor.VerifyCurrentLineTextAsync("public string? Title { get; set; }", HangMitigatingCancellationToken);
        }
    }
}
