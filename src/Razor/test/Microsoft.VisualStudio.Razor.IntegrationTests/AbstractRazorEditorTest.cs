// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Razor.IntegrationTests.InProcess;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xunit.Harness;

namespace Microsoft.VisualStudio.Razor.IntegrationTests
{
    public abstract class AbstractRazorEditorTest : AbstractEditorTest
    {
        private const string RazorComponentElementClassification = "RazorComponentElement";

        protected override string LanguageName => LanguageNames.Razor;

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            VisualStudioLogging.AddCustomLoggers();

            await TestServices.SolutionExplorer.CreateSolutionAsync("BlazorSolution", ControlledHangMitigatingCancellationToken);
            await TestServices.SolutionExplorer.AddProjectAsync("BlazorProject", WellKnownProjectTemplates.BlazorProject, groupId: WellKnownProjectTemplates.GroupIdentifiers.Server, templateId: null, LanguageName, ControlledHangMitigatingCancellationToken);
            await TestServices.SolutionExplorer.RestoreNuGetPackagesAsync(ControlledHangMitigatingCancellationToken);
            await TestServices.Workspace.WaitForProjectSystemAsync(ControlledHangMitigatingCancellationToken);

            await TestServices.Workspace.WaitForAsyncOperationsAsync(FeatureAttribute.LanguageServer, ControlledHangMitigatingCancellationToken);

            // We open the Index.razor file, and wait for 3 RazorComponentElement's to be classified, as that
            // way we know the LSP server is up, running, and has processed both local and library-sourced Components
            await TestServices.SolutionExplorer.AddFileAsync(RazorProjectConstants.BlazorProjectName, RazorProjectConstants.ModifiedIndexRazorFile, RazorProjectConstants.IndexPageContent, open: true, ControlledHangMitigatingCancellationToken);

            // Razor extension doesn't launch until a razor file is opened, so wait for it to equalize
            await TestServices.Workspace.WaitForAsyncOperationsAsync(FeatureAttribute.LanguageServer, ControlledHangMitigatingCancellationToken);
            await TestServices.Workspace.WaitForAsyncOperationsAsync(FeatureAttribute.Workspace, ControlledHangMitigatingCancellationToken);
            await TestServices.Workspace.WaitForProjectSystemAsync(ControlledHangMitigatingCancellationToken);

            await EnsureTextViewRolesAsync(ControlledHangMitigatingCancellationToken);
            await EnsureExtensionInstalledAsync(ControlledHangMitigatingCancellationToken);

            await TestServices.Editor.WaitForClassificationAsync(ControlledHangMitigatingCancellationToken, expectedClassification: RazorComponentElementClassification, count: 3);

            // Close the file we opened, just in case, so the test can start with a clean slate
            await TestServices.Editor.CloseDocumentWindowAsync(ControlledHangMitigatingCancellationToken);
        }

        private async Task EnsureTextViewRolesAsync(CancellationToken cancellationToken)
        {
            var textView = await TestServices.Editor.GetActiveTextViewAsync(cancellationToken);
            var contentType = textView.TextSnapshot.ContentType;
            Assert.AreEqual("Razor", contentType.TypeName);
        }

        private async Task EnsureExtensionInstalledAsync(CancellationToken cancellationToken)
        {
            const string AssemblyName = "Microsoft.AspNetCore.Razor.LanguageServer";
            using var semaphore = new SemaphoreSlim(1);
            await semaphore.WaitAsync(cancellationToken);

            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;

            var localAppData = Environment.GetEnvironmentVariable("LocalAppData");
            Assembly? assembly = null;
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                assembly = assemblies.FirstOrDefault((assembly) => assembly.GetName().Name.Equals(AssemblyName));
                if (assembly is null)
                {
                    await semaphore.WaitAsync(cancellationToken);
                }

                semaphore.Release();
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyLoad -= CurrentDomain_AssemblyLoad;
            }

            if (assembly is null)
            {
                throw new NotImplementedException($"Integration test did not load extension");
            }

            var version = assembly.GetName().Version;

            if (!version.Equals(new Version(42, 42, 42, 42)) || !assembly.Location.StartsWith(localAppData, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotImplementedException($"Integration test not running against Experimental Extension {assembly.Location}");
            }

            void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
            {
                if (args.LoadedAssembly.GetName().Name.Equals(AssemblyName, StringComparison.Ordinal))
                {
                    assembly = args.LoadedAssembly;
                    semaphore.Release();
                }
            }
        }
    }
}
