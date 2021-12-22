// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.VisualStudio.Razor.Integration.Test
{
    public static class WellKnownProjectTemplates
    {
        public const string BlazorProject = "Microsoft.WAP.CSharp.ASPNET.Blazor";

        public static class GroupIdentifiers
        {
            public const string Server = "Microsoft.Web.Blazor.Server";
            public const string Wasm = "Microsoft.Web.Blazor.Wasm";
        }

        public static class TemplateIdentifiers
        {
            public const string Server31 = "Microsoft.Web.Blazor.Server.CSharp.3.1";
            public const string Server50 = "Microsoft.Web.Blazor.Server.CSharp.5.0";
            public const string Server60 = "Microsoft.Web.Blazor.Server.CSharp.6.0";
        }
    }

    public static class LanguageNames
    {
        public const string Razor = "Razor";
        public const string CSharp = "CSharp";
        public const string VisualBasic = "VB";
    }

    public class RazorCodeActionsTests : AbstractRazorEditorTest
    {
        [IdeFact]
        public async Task CodeActions_Show()
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
