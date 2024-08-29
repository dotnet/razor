﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;

public abstract class LanguageServerTestBase : ToolingTestBase
{
    private protected IRazorSpanMappingService SpanMappingService { get; }
    private protected IFilePathService FilePathService { get; }
    private protected JsonSerializerOptions SerializerOptions { get; }

    protected LanguageServerTestBase(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        SpanMappingService = new ThrowingRazorSpanMappingService();

        SerializerOptions = new JsonSerializerOptions();
        // In its infinite wisdom, the LSP client has a public method that takes Newtonsoft.Json types, but an internal method that takes System.Text.Json types.
        typeof(VSInternalExtensionUtilities).GetMethod("AddVSInternalExtensionConverters", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, [SerializerOptions]);

        FilePathService = new LSPFilePathService(TestLanguageServerFeatureOptions.Instance);
    }

    private protected TestProjectSnapshotManager CreateProjectSnapshotManager()
        => CreateProjectSnapshotManager(ProjectEngineFactories.DefaultProvider);

    private protected TestProjectSnapshotManager CreateProjectSnapshotManager(
        IProjectEngineFactoryProvider projectEngineFactoryProvider)
        => new(
            projectEngineFactoryProvider,
            LoggerFactory,
            DisposalToken,
            initializer: static updater => updater.ProjectAdded(MiscFilesHostProject.Instance));

    private protected static RazorRequestContext CreateRazorRequestContext(
        DocumentContext? documentContext,
        ILspServices? lspServices = null)
        => new(documentContext, lspServices ?? StrictMock.Of<ILspServices>(), "lsp/method", uri: null);

    protected static RazorCodeDocument CreateCodeDocument(string text, ImmutableArray<TagHelperDescriptor> tagHelpers = default, string? filePath = null, string? rootNamespace = null)
    {
        filePath ??= "test.cshtml";

        var fileKind = FileKinds.GetFileKindFromFilePath(filePath);
        tagHelpers = tagHelpers.NullToEmpty();

        if (fileKind == FileKinds.Component)
        {
            tagHelpers = tagHelpers.AddRange(RazorTestResources.BlazorServerAppTagHelpers);
        }

        var sourceDocument = TestRazorSourceDocument.Create(text, filePath: filePath, relativePath: filePath);
        var projectEngine = RazorProjectEngine.Create(b =>
        {
            if (rootNamespace != null)
            {
                b.SetRootNamespace(rootNamespace);
            }

            RazorExtensions.Register(b);
        });
        var importDocumentName = fileKind == FileKinds.Legacy ? "_ViewImports.cshtml" : "_Imports.razor";
        var defaultImportDocument = TestRazorSourceDocument.Create(
            """
                @using BlazorApp1
                @using BlazorApp1.Pages
                @using BlazorApp1.Shared
                @using System;
                @using Microsoft.AspNetCore.Components
                @using Microsoft.AspNetCore.Components.Authorization
                @using Microsoft.AspNetCore.Components.Forms
                @using Microsoft.AspNetCore.Components.Routing
                @using Microsoft.AspNetCore.Components.Web
                """,
            RazorSourceDocumentProperties.Create(importDocumentName, importDocumentName));
        var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, fileKind, [defaultImportDocument], tagHelpers);
        return codeDocument;
    }

    private protected static IDocumentContextFactory CreateDocumentContextFactory(Uri documentPath, string sourceText)
    {
        var codeDocument = CreateCodeDocument(sourceText);
        return CreateDocumentContextFactory(documentPath, codeDocument);
    }

    private protected static DocumentContext CreateDocumentContext(Uri documentPath, RazorCodeDocument codeDocument)
    {
        return TestDocumentContext.From(documentPath.GetAbsoluteOrUNCPath(), codeDocument);
    }

    private protected static IDocumentContextFactory CreateDocumentContextFactory(
        Uri documentPath,
        RazorCodeDocument codeDocument,
        bool documentFound = true)
    {
        var documentContextFactory = documentFound
            ? new TestDocumentContextFactory(documentPath.GetAbsoluteOrUNCPath(), codeDocument)
            : new TestDocumentContextFactory();

        return documentContextFactory;
    }

    private protected static DocumentContext CreateDocumentContext(Uri uri, IDocumentSnapshot snapshot)
    {
        return new DocumentContext(uri, snapshot, projectContext: null);
    }

    private protected static RazorLSPOptionsMonitor GetOptionsMonitor(bool enableFormatting = true, bool autoShowCompletion = true, bool autoListParams = true, bool formatOnType = true, bool autoInsertAttributeQuotes = true, bool colorBackground = false, bool codeBlockBraceOnNextLine = false, bool commitElementsWithSpace = true)
    {
        var configService = StrictMock.Of<IConfigurationSyncService>();

        var options = new RazorLSPOptions(enableFormatting, true, InsertSpaces: true, TabSize: 4, autoShowCompletion, autoListParams, formatOnType, autoInsertAttributeQuotes, colorBackground, codeBlockBraceOnNextLine, commitElementsWithSpace);
        var optionsMonitor = new RazorLSPOptionsMonitor(configService, options);
        return optionsMonitor;
    }

    private class ThrowingRazorSpanMappingService : IRazorSpanMappingService
    {
        public Task<ImmutableArray<RazorMappedSpanResult>> MapSpansAsync(Document document, IEnumerable<TextSpan> spans, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
