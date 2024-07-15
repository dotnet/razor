// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Basic.Reference.Assemblies;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Remote.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Composition;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public abstract class CohostTestBase(ITestOutputHelper testOutputHelper) : WorkspaceTestBase(testOutputHelper)
{
    private TestRemoteServiceInvoker? _remoteServiceInvoker;

    private protected TestRemoteServiceInvoker RemoteServiceInvoker => _remoteServiceInvoker.AssumeNotNull();

    protected override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Create a new isolated MEF composition but use the cached configuration for performance.
        var configuration = await RemoteMefComposition.GetConfigurationAsync(DisposalToken);
        var runtimeComposition = RuntimeComposition.CreateRuntimeComposition(configuration);
        var exportProviderFactory = runtimeComposition.CreateExportProviderFactory();
        var exportProvider = exportProviderFactory.CreateExportProvider();

        AddDisposable(exportProvider);

        _remoteServiceInvoker = new TestRemoteServiceInvoker(JoinableTaskContext, exportProvider, LoggerFactory);
        AddDisposable(_remoteServiceInvoker);
    }

    protected TextDocument CreateProjectAndRazorDocument(string contents)
    {
        var projectFilePath = TestProjectData.SomeProject.FilePath;
        var documentFilePath = TestProjectData.SomeProjectComponentFile1.FilePath;
        var projectName = Path.GetFileNameWithoutExtension(projectFilePath);
        var projectId = ProjectId.CreateNewId(debugName: projectName);
        var documentId = DocumentId.CreateNewId(projectId, debugName: documentFilePath);

        var projectInfo = ProjectInfo
            .Create(
                projectId,
                VersionStamp.Create(),
                name: projectName,
                assemblyName: projectName,
                LanguageNames.CSharp,
                documentFilePath)
            .WithMetadataReferences(AspNet80.ReferenceInfos.All.Select(r => r.Reference));

        var solution = Workspace.CurrentSolution.AddProject(projectInfo);

        solution = solution
            .AddAdditionalDocument(
                documentId,
                documentFilePath,
                SourceText.From(contents),
                filePath: documentFilePath)
            .AddAdditionalDocument(
                DocumentId.CreateNewId(projectId),
                name: "_Imports.razor",
                text: SourceText.From("""
                    @using Microsoft.AspNetCore.Components
                    @using Microsoft.AspNetCore.Components.Authorization
                    @using Microsoft.AspNetCore.Components.Routing
                    @using Microsoft.AspNetCore.Components.Web
                    """),
                filePath: TestProjectData.SomeProjectComponentImportFile1.FilePath);

        return solution.GetAdditionalDocument(documentId).AssumeNotNull();
    }
}
