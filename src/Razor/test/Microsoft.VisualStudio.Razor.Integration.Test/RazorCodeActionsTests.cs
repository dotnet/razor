// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Razor.Integration.Test
{
    public class RazorCodeActionsTests : AbstractRazorEditorTest
    {
        [IdeFact(Skip = "Broken in Preveiw1")]
        public async Task RazorCodeActions_Show()
        {
            // Create Warnings by removing usings
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, ImportsRazorFile, HangMitigatingCancellationToken);
            await TestServices.Editor.SetTextAsync("", HangMitigatingCancellationToken);

            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, CounterRazorFile, HangMitigatingCancellationToken);

            await TestServices.Editor.SetTextAsync("<SurveyPrompt></SurveyPrompt>", HangMitigatingCancellationToken);
            await TestServices.Editor.MoveCaretAsync(3, HangMitigatingCancellationToken);

            // Act
            var codeActions = await TestServices.Editor.InvokeCodeActionListAsync(HangMitigatingCancellationToken);

            var codeActionSet = Assert.Single(codeActions);
            Assert.Contains(codeActionSet.Actions, a => a.DisplayText.Equals($"@using {BlazorProjectName}.Shared"));
        }

    }
}
