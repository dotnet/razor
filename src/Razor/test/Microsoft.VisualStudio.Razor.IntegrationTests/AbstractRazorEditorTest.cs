// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Razor.IntegrationTests.InProcess;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    public abstract class AbstractRazorEditorTest : AbstractEditorTest
    {
        internal const string BlazorProjectName = "BlazorProject";

        private static readonly string s_pagesDir = Path.Combine("Pages");
        private static readonly string s_sharedDir = Path.Combine("Shared");
        internal static readonly string CounterRazorFile = Path.Combine(s_pagesDir, "Counter.razor");
        internal static readonly string IndexRazorFile = Path.Combine(s_pagesDir, "Index.razor");
        internal static readonly string SemanticTokensFile = Path.Combine(s_pagesDir, "SemanticTokens.razor");
        internal static readonly string MainLayoutFile = Path.Combine(s_sharedDir, "MainLayout.razor");
        internal static readonly string ImportsRazorFile = "_Imports.razor";

        protected override string LanguageName => LanguageNames.Razor;

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            await TestServices.SolutionExplorer.CreateSolutionAsync("BlazorSolution", HangMitigatingCancellationToken);
            await TestServices.SolutionExplorer.AddProjectAsync("BlazorProject", WellKnownProjectTemplates.BlazorProject, groupId: WellKnownProjectTemplates.GroupIdentifiers.Server, templateId: null, LanguageName, HangMitigatingCancellationToken);
            await TestServices.SolutionExplorer.RestoreNuGetPackagesAsync(HangMitigatingCancellationToken);
            await TestServices.Workspace.WaitForProjectSystemAsync(HangMitigatingCancellationToken);

            await TestServices.Workspace.WaitForAsyncOperationsAsync(FeatureAttribute.LanguageServer, HangMitigatingCancellationToken);

            // We open the Index.razor file, and wait for the SurveyPrompt component to be classified, as that
            // way we know the LSP server is up and running and responding
            await TestServices.SolutionExplorer.OpenFileAsync(BlazorProjectName, IndexRazorFile, HangMitigatingCancellationToken);
            await TestServices.Editor.WaitForClassificationAsync(HangMitigatingCancellationToken, expectedClassification: "RazorComponentElement");

            // Close the file we opened, just in case, so the test can start with a clean slate
            await TestServices.Editor.CloseDocumentWindowAsync(HangMitigatingCancellationToken);
        }
    }
}
