// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.VisualStudio.Razor.IntegrationTests.InProcess;
using Xunit.Harness;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    public abstract class AbstractRazorEditorTest : AbstractEditorTest
    {
        internal const string BlazorProjectName = "BlazorProject";

        private static readonly string s_pagesDir = Path.Combine("Pages");
        private static readonly string s_sharedDir = Path.Combine("Shared");
        internal static readonly string FetchDataRazorFile = Path.Combine(s_pagesDir, "FetchData.razor");
        internal static readonly string CounterRazorFile = Path.Combine(s_pagesDir, "Counter.razor");
        internal static readonly string IndexRazorFile = Path.Combine(s_pagesDir, "Index.razor");
        internal static readonly string ModifiedIndexRazorFile = Path.Combine(s_pagesDir, "ModifiedIndex.razor");
        internal static readonly string SemanticTokensFile = Path.Combine(s_pagesDir, "SemanticTokens.razor");
        internal static readonly string MainLayoutFile = Path.Combine(s_sharedDir, "MainLayout.razor");
        internal static readonly string ErrorCshtmlFile = Path.Combine(s_pagesDir, "Error.cshtml");
        internal static readonly string ImportsRazorFile = "_Imports.razor";

        internal static readonly string IndexPageContent = @"@page ""/""

<PageTitle>Index</PageTitle>

<h1>Hello, world!</h1>

Welcome to your new app.

<SurveyPrompt Title=""How is Blazor working for you?"" />";

        internal static readonly string MainLayoutContent = @"@inherits LayoutComponentBase

<PageTitle>BlazorApp</PageTitle>

<div class=""page"">
    <div class=""sidebar"">
        <NavMenu />
    </div>

    <main>
        <div class=""top-row px-4"">
            <a href=""https://docs.microsoft.com/aspnet/"" target=""_blank"">About</a>
        </div>

        <article class=""content px-4"">
            @Body
        </article>
    </main>
</div>
";

        private const string RazorComponentElementClassification = "RazorComponentElement";
        private const string RazorOutputLogId = "RazorOutputLog";

        protected override string LanguageName => LanguageNames.Razor;

        private static bool s_customLoggersAdded = false;

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            // Add custom logs on failure if they haven't already been.
            if (!s_customLoggersAdded)
            {
                DataCollectionService.RegisterCustomLogger(RazorOutputPaneLogger, RazorOutputLogId, "log");

                s_customLoggersAdded = true;
            }

            await TestServices.SolutionExplorer.CreateSolutionAsync("BlazorSolution", HangMitigatingCancellationToken);
            await TestServices.SolutionExplorer.AddProjectAsync("BlazorProject", WellKnownProjectTemplates.BlazorProject, groupId: WellKnownProjectTemplates.GroupIdentifiers.Server, templateId: null, LanguageName, HangMitigatingCancellationToken);
            await TestServices.SolutionExplorer.RestoreNuGetPackagesAsync(HangMitigatingCancellationToken);
            await TestServices.Workspace.WaitForProjectSystemAsync(HangMitigatingCancellationToken);

            await TestServices.Workspace.WaitForAsyncOperationsAsync(FeatureAttribute.LanguageServer, HangMitigatingCancellationToken);

            // We open the Index.razor file, and wait for 3 RazorComponentElement's to be classified, as that
            // way we know the LSP server is up, running, and has processed both local and library-sourced Components
            await TestServices.SolutionExplorer.AddFileAsync(BlazorProjectName, ModifiedIndexRazorFile, IndexPageContent, open: true, HangMitigatingCancellationToken);
            await TestServices.Editor.WaitForClassificationAsync(HangMitigatingCancellationToken, expectedClassification: RazorComponentElementClassification, count: 3);

            // Close the file we opened, just in case, so the test can start with a clean slate
            await TestServices.Editor.CloseDocumentWindowAsync(HangMitigatingCancellationToken);

            async void RazorOutputPaneLogger(string filePath)
            {
                var paneContent = await TestServices.Output.GetRazorOutputPaneContentAsync(CancellationToken.None);
                File.WriteAllText(filePath, paneContent);
            }
        }
    }
}
