// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    public class RazorCodeActionsTests : AbstractRazorEditorTest
    {
        [IdeFact(Skip = "Behavior not yet testable")]
        public async Task RazorCodeActions_Show()
        {
            // Create Warnings by removing usings
            await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.ImportsRazorFile, HangMitigatingCancellationToken);
            await TestServices.Editor.SetTextAsync("", HangMitigatingCancellationToken);

            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.CounterRazorFile, HangMitigatingCancellationToken);

            await TestServices.Editor.SetTextAsync("<SurveyPrompt></SurveyPrompt>", HangMitigatingCancellationToken);
            await TestServices.Editor.MoveCaretAsync(3, HangMitigatingCancellationToken);

            // Act
            var codeActions = await TestServices.Editor.InvokeCodeActionListAsync(HangMitigatingCancellationToken);

            // Assert
            var codeActionSet = Assert.Single(codeActions);
            var usingString = $"@using {RazorProjectConstants.BlazorProjectName}.Shared";
            var codeAction = Assert.Single(codeActionSet.Actions, a => a.DisplayText.Equals(usingString));

            await TestServices.Editor.InvokeCodeActionAsync(codeAction, HangMitigatingCancellationToken);

            await TestServices.Editor.VerifyTextContainsAsync(usingString, HangMitigatingCancellationToken);
        }
    }
}
