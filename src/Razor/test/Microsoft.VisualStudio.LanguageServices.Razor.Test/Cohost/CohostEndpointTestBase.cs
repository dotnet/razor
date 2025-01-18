﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Basic.Reference.Assemblies;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Remote.Razor;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Remote.Razor.SemanticTokens;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public abstract class CohostEndpointTestBase(ITestOutputHelper testOutputHelper) : ToolingTestBase(testOutputHelper)
{
    private const string CSharpVirtualDocumentSuffix = ".g.cs";
    private ExportProvider? _exportProvider;
    private TestRemoteServiceInvoker? _remoteServiceInvoker;
    private RemoteClientInitializationOptions _clientInitializationOptions;
    private RemoteClientLSPInitializationOptions _clientLSPInitializationOptions;
    private IFilePathService? _filePathService;

    private protected TestRemoteServiceInvoker RemoteServiceInvoker => _remoteServiceInvoker.AssumeNotNull();
    private protected IFilePathService FilePathService => _filePathService.AssumeNotNull();
    private protected RemoteLanguageServerFeatureOptions FeatureOptions => OOPExportProvider.GetExportedValue<RemoteLanguageServerFeatureOptions>();
    private protected RemoteClientCapabilitiesService ClientCapabilitiesService => OOPExportProvider.GetExportedValue<RemoteClientCapabilitiesService>();
    private protected RemoteSemanticTokensLegendService SemanticTokensLegendService => OOPExportProvider.GetExportedValue<RemoteSemanticTokensLegendService>();

    /// <summary>
    /// The export provider for Razor OOP services (not Roslyn)
    /// </summary>
    private protected ExportProvider OOPExportProvider => _exportProvider.AssumeNotNull();

    protected override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // Create a new isolated MEF composition.
        // Note that this uses a cached catalog and configuration for performance.
        try
        {
            _exportProvider = await RemoteMefComposition.CreateExportProviderAsync(DisposalToken);
        }
        catch (CompositionFailedException ex) when (ex.Errors is not null)
        {
            Assert.Fail($"""
                Errors in the Remote MEF composition:

                {string.Join(Environment.NewLine, ex.Errors.SelectMany(e => e).Select(e => e.Message))}
                """);
        }

        AddDisposable(_exportProvider);

        _remoteServiceInvoker = new TestRemoteServiceInvoker(JoinableTaskContext, _exportProvider, LoggerFactory);
        AddDisposable(_remoteServiceInvoker);

        _clientInitializationOptions = new()
        {
            CSharpVirtualDocumentSuffix = CSharpVirtualDocumentSuffix,
            HtmlVirtualDocumentSuffix = ".g.html",
            IncludeProjectKeyInGeneratedFilePath = false,
            UsePreciseSemanticTokenRanges = false,
            UseRazorCohostServer = true,
            ReturnCodeActionAndRenamePathsWithPrefixedSlash = false,
            ForceRuntimeCodeGeneration = false,
            SupportsFileManipulation = true,
            ShowAllCSharpCodeActions = false,
            UseNewFormattingEngine = false,
            SupportsSoftSelectionInCompletion = true,
        };
        UpdateClientInitializationOptions(c => c);

        var completionSetting = new CompletionSetting
        {
            CompletionItem = new CompletionItemSetting(),
            CompletionItemKind = new CompletionItemKindSetting()
            {
                ValueSet = (CompletionItemKind[])Enum.GetValues(typeof(CompletionItemKind)),
            },
            CompletionListSetting = new CompletionListSetting()
            {
                ItemDefaults = ["commitCharacters", "editRange", "insertTextFormat"]
            },
            ContextSupport = false,
            InsertTextMode = InsertTextMode.AsIs,
        };

        _clientLSPInitializationOptions = new()
        {
            ClientCapabilities = new VSInternalClientCapabilities()
            {
                SupportsVisualStudioExtensions = true,
                TextDocument = new TextDocumentClientCapabilities
                {
                    Completion = completionSetting
                }
            },
            TokenTypes = [],
            TokenModifiers = []
        };
        UpdateClientLSPInitializationOptions(c => c);

        _filePathService = new RemoteFilePathService(FeatureOptions);
    }

    private protected void UpdateClientInitializationOptions(Func<RemoteClientInitializationOptions, RemoteClientInitializationOptions> mutation)
    {
        _clientInitializationOptions = mutation(_clientInitializationOptions);
        FeatureOptions.SetOptions(_clientInitializationOptions);
    }

    private protected void UpdateClientLSPInitializationOptions(Func<RemoteClientLSPInitializationOptions, RemoteClientLSPInitializationOptions> mutation)
    {
        _clientLSPInitializationOptions = mutation(_clientLSPInitializationOptions);
        ClientCapabilitiesService.SetCapabilities(_clientLSPInitializationOptions.ClientCapabilities);
        SemanticTokensLegendService.SetLegend(_clientLSPInitializationOptions.TokenTypes, _clientLSPInitializationOptions.TokenModifiers);
    }

    private protected virtual TestComposition ConfigureRoslynDevenvComposition(TestComposition composition)
        => composition;

    protected async Task<TextDocument> CreateProjectAndRazorDocumentAsync(
        string contents,
        string? fileKind = null,
        (string fileName, string contents)[]? additionalFiles = null,
        bool createSeparateRemoteAndLocalWorkspaces = false,
        bool inGlobalNamespace = false)
    {
        // Using IsLegacy means null == component, so easier for test authors
        var isComponent = !FileKinds.IsLegacy(fileKind);

        var documentFilePath = isComponent
            ? TestProjectData.SomeProjectComponentFile1.FilePath
            : TestProjectData.SomeProjectFile1.FilePath;

        var projectFilePath = TestProjectData.SomeProject.FilePath;
        var projectName = Path.GetFileNameWithoutExtension(projectFilePath);
        var projectId = ProjectId.CreateNewId(debugName: projectName);
        var documentId = DocumentId.CreateNewId(projectId, debugName: documentFilePath);

        var remoteWorkspace = RemoteWorkspaceAccessor.GetWorkspace();
        var remoteDocument = await CreateProjectAndRazorDocumentAsync(remoteWorkspace, projectId, projectName, documentId, documentFilePath, contents, additionalFiles, inGlobalNamespace);

        if (createSeparateRemoteAndLocalWorkspaces)
        {
            // Usually its fine to just use the remote workspace, but sometimes we need to also have things available in the
            // "devenv" side of Roslyn, which is a different workspace with a different set of services. We don't have any
            // actual solution syncing set up for testing, and don't really use a service broker, but since we also would
            // expect to never make changes to a workspace, it should be fine to simply create duplicated solutions as part
            // of test setup.
            return await CreateLocalProjectAndRazorDocumentAsync(
                remoteDocument.Project.Solution,
                projectId,
                projectName,
                documentId,
                documentFilePath,
                contents,
                additionalFiles,
                inGlobalNamespace);
        }

        // If we're just creating one workspace, then its the remote one and we just return the remote document
        // and assume that the endpoint under test doesn't need to do anything on the devenv side. This makes it
        // easier for tests to mutate solutions
        return remoteDocument;
    }

    private async Task<TextDocument> CreateLocalProjectAndRazorDocumentAsync(
        Solution remoteSolution,
        ProjectId projectId,
        string projectName,
        DocumentId documentId,
        string documentFilePath,
        string contents,
        (string fileName, string contents)[]? additionalFiles,
        bool inGlobalNamespace)
    {
        var exportProvider = ConfigureRoslynDevenvComposition(TestComposition.Roslyn).ExportProviderFactory.CreateExportProvider();
        AddDisposable(exportProvider);
        var workspace = TestWorkspace.CreateWithDiagnosticAnalyzers(exportProvider);
        AddDisposable(workspace);

        var razorDocument = await CreateProjectAndRazorDocumentAsync(workspace, projectId, projectName, documentId, documentFilePath, contents, additionalFiles, inGlobalNamespace);

        // If we're creating remote and local workspaces, then we'll return the local document, and have to allow
        // the remote service invoker to map from the local solution to the remote one.
        RemoteServiceInvoker.MapSolutionIdToRemote(razorDocument.Project.Solution.Id, remoteSolution);

        return razorDocument;
    }

    private async Task<TextDocument> CreateProjectAndRazorDocumentAsync(CodeAnalysis.Workspace workspace, ProjectId projectId, string projectName, DocumentId documentId, string documentFilePath, string contents, (string fileName, string contents)[]? additionalFiles, bool inGlobalNamespace)
    {
        var projectInfo = ProjectInfo
            .Create(
                projectId,
                VersionStamp.Create(),
                name: projectName,
                assemblyName: projectName,
                LanguageNames.CSharp,
                documentFilePath,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(AspNet80.ReferenceInfos.All.Select(r => r.Reference));

        if (!inGlobalNamespace)
        {
            projectInfo = projectInfo.WithDefaultNamespace(TestProjectData.SomeProject.RootNamespace);
        }

        var solution = workspace.CurrentSolution.AddProject(projectInfo);

        solution = solution
            .AddAdditionalDocument(
                documentId,
                documentFilePath,
                SourceText.From(contents),
                filePath: documentFilePath)
            .AddAdditionalDocument(
                DocumentId.CreateNewId(projectId),
                name: TestProjectData.SomeProjectComponentImportFile1.FilePath,
                text: SourceText.From("""
                    @using Microsoft.AspNetCore.Components
                    @using Microsoft.AspNetCore.Components.Authorization
                    @using Microsoft.AspNetCore.Components.Forms
                    @using Microsoft.AspNetCore.Components.Routing
                    @using Microsoft.AspNetCore.Components.Web
                    """),
                filePath: TestProjectData.SomeProjectComponentImportFile1.FilePath)
            .AddAdditionalDocument(
                DocumentId.CreateNewId(projectId),
                name: "_ViewImports.cshtml",
                text: SourceText.From("""
                    @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
                    """),
                filePath: TestProjectData.SomeProjectImportFile.FilePath);

        if (additionalFiles is not null)
        {
            foreach (var file in additionalFiles)
            {
                solution = Path.GetExtension(file.fileName) == ".cs"
                    ? solution.AddDocument(DocumentId.CreateNewId(projectId), name: file.fileName, text: SourceText.From(file.contents), filePath: file.fileName)
                    : solution.AddAdditionalDocument(DocumentId.CreateNewId(projectId), name: file.fileName, text: SourceText.From(file.contents), filePath: file.fileName);
            }
        }

        // Until the source generator is hooked up, the workspace representing "local" projects doesn't have anything
        // to actually compile the Razor to C#, so we just do it now at creation
        var snapshotManager = _exportProvider.AssumeNotNull().GetExportedValue<RemoteSnapshotManager>();
        solution = await CompileRazorDocumentAsync(snapshotManager, documentId, solution, DisposalToken);

        if (additionalFiles is not null)
        {
            foreach (var file in additionalFiles)
            {
                if (Path.GetExtension(file.fileName) is ".cshtml" or ".razor" &&
                    Path.GetFileNameWithoutExtension(file.fileName) is not ("_ViewImports" or "_Imports"))
                {
                    var additionalDocumentId = solution.GetDocumentIdsWithFilePath(file.fileName).Single();
                    solution = await CompileRazorDocumentAsync(snapshotManager, additionalDocumentId, solution, DisposalToken);
                }
            }
        }

        return solution.GetAdditionalDocument(documentId).AssumeNotNull();

        static async Task<Solution> CompileRazorDocumentAsync(RemoteSnapshotManager snapshotManager, DocumentId documentId, Solution solution, CancellationToken cancellationToken)
        {
            // We're cheating a bit here and using the remote export provider to get something to do the compilation
            var razorDocument = solution.GetAdditionalDocument(documentId).AssumeNotNull();
            var snapshot = snapshotManager.GetSnapshot(razorDocument);
            // Compile the Razor file
            var codeDocument = await snapshot.GetGeneratedOutputAsync(cancellationToken);
            // Update the generated doc contents
            var filePath = razorDocument.FilePath + CSharpVirtualDocumentSuffix;
            var generatedDocumentIds = solution.GetDocumentIdsWithFilePath(filePath);
            if (generatedDocumentIds.Length == 0)
            {
                var generatedDocumentId = DocumentId.CreateNewId(documentId.ProjectId);
                solution = solution.AddDocument(generatedDocumentId, name: filePath, text: SourceText.From(""), filePath: filePath);
                generatedDocumentIds = solution.GetDocumentIdsWithFilePath(filePath);
            }

            return solution.WithDocumentText(generatedDocumentIds, codeDocument.GetCSharpSourceText());
        }
    }

    protected static Uri FileUri(string projectRelativeFileName)
        => new(FilePath(projectRelativeFileName));

    protected static string FilePath(string projectRelativeFileName)
        => Path.GetFullPath(Path.Combine(TestProjectData.SomeProjectPath, projectRelativeFileName));
}
