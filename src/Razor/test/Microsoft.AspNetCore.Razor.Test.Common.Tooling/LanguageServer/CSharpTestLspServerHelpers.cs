﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;

internal static class CSharpTestLspServerHelpers
{
    private const string EditRangeSetting = "editRange";

    public static Task<CSharpTestLspServer> CreateCSharpLspServerAsync(
        SourceText csharpSourceText,
        Uri csharpDocumentUri,
        VSInternalServerCapabilities serverCapabilities,
        CancellationToken cancellationToken) =>
        CreateCSharpLspServerAsync(csharpSourceText, csharpDocumentUri, serverCapabilities, new EmptyMappingService(), capabilitiesUpdater: null, cancellationToken);

    public static Task<CSharpTestLspServer> CreateCSharpLspServerAsync(
        SourceText csharpSourceText,
        Uri csharpDocumentUri,
        VSInternalServerCapabilities serverCapabilities,
        IRazorSpanMappingService razorSpanMappingService,
        Action<VSInternalClientCapabilities> capabilitiesUpdater,
        CancellationToken cancellationToken)
    {
        var files = new[]
        {
            (csharpDocumentUri, csharpSourceText)
        };

        return CreateCSharpLspServerAsync(files, serverCapabilities, razorSpanMappingService, multiTargetProject: true, capabilitiesUpdater, cancellationToken);
    }

    public static async Task<CSharpTestLspServer> CreateCSharpLspServerAsync(
        IEnumerable<(Uri Uri, SourceText SourceText)> files,
        VSInternalServerCapabilities serverCapabilities,
        IRazorSpanMappingService razorSpanMappingService,
        bool multiTargetProject,
         Action<VSInternalClientCapabilities> capabilitiesUpdater,
        CancellationToken cancellationToken)
    {
        var csharpFiles = files.Select(f => new CSharpFile(f.Uri, f.SourceText));

        var exportProvider = TestComposition.Roslyn.ExportProviderFactory.CreateExportProvider();
        var metadataReferences = (await ReferenceAssemblies.Default.ResolveAsync(language: LanguageNames.CSharp, cancellationToken))
            // ComponentBase here comes from our ComponentShim project, not the real ASP.NET libraries. It's enough for the generated C#
            // in tests to at least compile better.
            .Add(ReferenceUtil.AspNetLatestComponents);
        var workspace = CreateCSharpTestWorkspace(csharpFiles, exportProvider, metadataReferences, razorSpanMappingService, multiTargetProject);
        var clientCapabilities = new VSInternalClientCapabilities
        {
            SupportsVisualStudioExtensions = true,
            TextDocument = new TextDocumentClientCapabilities
            {
                Completion = new VSInternalCompletionSetting
                {
                    CompletionListSetting = new()
                    {
                        ItemDefaults = [EditRangeSetting]
                    },
                    CompletionItem = new()
                    {
                        SnippetSupport = true
                    }
                },
                InlayHint = new()
                {
                    ResolveSupport = new InlayHintResolveSupportSetting { Properties = ["tooltip"] }
                }
            },
            SupportsDiagnosticRequests = true,
            Workspace = new()
            {
                Configuration = true
            }
        };

        capabilitiesUpdater?.Invoke(clientCapabilities);

        return await CSharpTestLspServer.CreateAsync(
            workspace, exportProvider, clientCapabilities, serverCapabilities, cancellationToken);
    }

    private static AdhocWorkspace CreateCSharpTestWorkspace(
        IEnumerable<CSharpFile> files,
        ExportProvider exportProvider,
        ImmutableArray<MetadataReference> metadataReferences,
        IRazorSpanMappingService razorSpanMappingService,
        bool multiTargetProject)
    {
        var workspace = TestWorkspace.CreateWithDiagnosticAnalyzers(exportProvider);

        // Add project and solution to workspace
        var projectInfoNet60 = ProjectInfo.Create(
            id: ProjectId.CreateNewId("TestProject (net6.0)"),
            version: VersionStamp.Default,
            name: "TestProject (net6.0)",
            assemblyName: "TestProject.dll",
            language: LanguageNames.CSharp,
            filePath: @"C:\TestSolution\TestProject.csproj",
            metadataReferences: metadataReferences).WithCompilationOutputInfo(new CompilationOutputInfo().WithAssemblyPath(@"C:\TestSolution\obj\TestProject.dll"));

        var projectInfoNet80 = ProjectInfo.Create(
            id: ProjectId.CreateNewId("TestProject (net8.0)"),
            version: VersionStamp.Default,
            name: "TestProject (net8.0)",
            assemblyName: "TestProject.dll",
            language: LanguageNames.CSharp,
            filePath: @"C:\TestSolution\TestProject.csproj",
            metadataReferences: metadataReferences);

        ProjectInfo[] projectInfos = multiTargetProject
            ? [projectInfoNet60, projectInfoNet80]
            : [projectInfoNet80];

        foreach (var projectInfo in projectInfos)
        {
            workspace.AddProject(projectInfo);
        }

        // Add document to workspace. We use an IVT method to create the DocumentInfo variable because there's
        // a special constructor in Roslyn that will help identify the document as belonging to Razor.
        var languageServerFactory = exportProvider.GetExportedValue<AbstractRazorLanguageServerFactoryWrapper>();

        var documentCount = 0;
        foreach (var (documentUri, csharpSourceText) in files)
        {
            var documentFilePath = documentUri.GetDocumentFilePath();
            var textAndVersion = TextAndVersion.Create(csharpSourceText, VersionStamp.Default, documentFilePath);

            foreach (var projectInfo in projectInfos)
            {
                var documentInfo = languageServerFactory.CreateDocumentInfo(
                    id: DocumentId.CreateNewId(projectInfo.Id),
                    name: "TestDocument" + documentCount,
                    filePath: documentFilePath,
                    loader: TextLoader.From(textAndVersion),
                    razorDocumentServiceProvider: new TestRazorDocumentServiceProvider(razorSpanMappingService));

                workspace.AddDocument(documentInfo);
            }

            documentCount++;
        }

        return workspace;
    }

    private record CSharpFile(Uri DocumentUri, SourceText CSharpSourceText);

    private class EmptyMappingService : IRazorSpanMappingService
    {
        public Task<ImmutableArray<RazorMappedSpanResult>> MapSpansAsync(Document document, IEnumerable<TextSpan> spans, CancellationToken cancellationToken)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorMappedSpanResult>();
        }
    }
}
