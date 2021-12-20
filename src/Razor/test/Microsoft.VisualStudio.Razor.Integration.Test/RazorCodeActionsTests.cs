// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Razor.Integration.Test
{
    public static class WellKnownProjectTemplates
    {
        public const string BlazorProject = "Microsoft.WAP.CSharp.ASPNET.Blazor";
    }

    public static class LanguageNames
    {
        public const string Razor = "Razor";
        public const string CSharp = "CSharp";
        public const string VisualBasic = "VB";
    }

    public class RazorCodeActionsTests : RazorEditorTestAbstract
    {
        [IdeFact]
        public async Task CodeActions_Show()
        {
            // Open the file
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, CounterRazorFile, HangMitigatingCancellationToken);

            await TestServices.Editor.PlaceCaretAsync("</button>", charsOffset: 1, HangMitigatingCancellationToken);
            await TestServices.Editor.SetTextAsync("<SurveyPrompt></SurveyPrompt>", HangMitigatingCancellationToken);
            await TestServices.Editor.MoveCaretAsync(3, HangMitigatingCancellationToken);

            // Act
            await TestServices.Editor.InvokeCodeActionListAsync(HangMitigatingCancellationToken);

            // Assert
            await TestServices.Editor.IsLightBulbSessionExpandedAsync(HangMitigatingCancellationToken);
        }
    }
}
