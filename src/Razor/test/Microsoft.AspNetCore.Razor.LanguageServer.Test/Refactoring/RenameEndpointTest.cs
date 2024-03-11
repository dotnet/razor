// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Protocol;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring;

[UseExportProvider]
public class RenameEndpointTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    private const string ProjectFilePath1 = "c:/First/First.csproj";
    private const string ProjectFilePath2 = "c:/Second/Second.csproj";

    private const string IntermediateOutputPath1 = "c:/First/obj";
    private const string IntermediateOutputPath2 = "c:/Second/obj";

    private const string RootNamespace1 = "First.Components";
    private const string RootNamespace2 = "Second.Components";

    private const string ComponentFilePath1 = "c:/First/Component1.razor";
    private const string ComponentText1 = $"""
        @namespace {RootNamespace1}
        @using Test
        <Component2></Component2>
        """;

    private const string ComponentFilePath2 = "c:/First/Component2.razor";
    private const string ComponentText2 = """
        @namespace Test
        """;

    private const string ComponentFilePath1337 = "c:/First/Component1337.razor";
    private const string ComponentText1337 = """
        @namespace Test
        """;

    private const string ComponentFilePath3 = "c:/Second/Component3.razor";
    private const string ComponentText3 = $"""
        @namespace {RootNamespace2}
        <Component3></Component3>
        """;

    private const string ComponentFilePath4 = "c:/Second/Component4.razor";
    private const string ComponentText4 = $"""
        @namespace {RootNamespace2}
        <Component3></Component3>
        <Component3></Component3>
        """;

    private const string ComponentFilePath5 = "c:/Second/Component5.razor";

    private const string ComponentWithParamFilePath = "c:/Second/ComponentWithParam.razor";
    private const string ComponentWithParamText = $"""
        @namespace {RootNamespace2}
        <Component3 Title="Something"></Component3>
        """;

    private const string IndexFilePath1 = "c:/First/Index.razor";
    private const string IndexText1 = $"""
        @namespace {RootNamespace1}
        @using Test
        <Component1337></Component1337>
        <Test.Component1337></Test.Component1337>
        """;

    private const string DirectoryFilePath1 = "c:/First/Directory1.razor";
    private const string DirectoryText1 = """
        @namespace Test.Components
        <Directory2></Directory2>
        """;

    private const string DirectoryFilePath2 = "c:/First/Directory2.razor";
    private const string DirectoryText2 = """
        @namespace Test.Components
        <Directory1></Directory1>
        """;

    private string GetFileUriString(string filePath) => $"file:///{filePath}";
    private Uri GetFileUri(string filePath) => new(GetFileUriString(filePath));

    [Fact]
    public async Task Handle_Rename_FileManipulationNotSupported_ReturnsNull()
    {
        // Arrange
        var options = StrictMock.Of<LanguageServerFeatureOptions>(o =>
            o.SupportsFileManipulation == false &&
            o.ReturnCodeActionAndRenamePathsWithPrefixedSlash == false);
        var (endpoint, documentContextFactory) = await CreateEndpointAndDocumentContextFactoryAsync(options);
        var uri = GetFileUri(ComponentFilePath1);
        var request = new RenameParams
        {
            TextDocument = new() { Uri = uri },
            Position = new Position(2, 1),
            NewName = "Component5"
        };

        var documentContext = await GetDocumentContextAsync(documentContextFactory, uri);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_Rename_WithNamespaceDirective()
    {
        // Arrange
        var (endpoint, documentContextFactory) = await CreateEndpointAndDocumentContextFactoryAsync();
        var uri = new Uri(ComponentFilePath1);
        var request = new RenameParams
        {
            TextDocument = new() { Uri = uri },
            Position = new Position(2, 1),
            NewName = "Component5"
        };

        var documentContext = await GetDocumentContextAsync(documentContextFactory, uri);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(result);
        var documentChanges = result.DocumentChanges.AssumeNotNull();
        Assert.Equal(2, documentChanges.Count());

        // We renamed Component2 to Component5, so ensure we received a file rename.
        var renameChange = documentChanges.ElementAt(0);
        Assert.True(renameChange.TryGetThird(out var renameFile));
        Assert.Equal(GetFileUri(ComponentFilePath2), renameFile.OldUri);
        Assert.Equal(GetFileUri("c:/First/Component5.razor"), renameFile.NewUri);

        // Next, we should get a series of text edits to Component1 that rename
        // "Component2" to "Component5".
        var editChange = documentChanges.ElementAt(1);
        Assert.True(editChange.TryGetFirst(out var textDocumentEdit));
        Assert.Equal(uri, textDocumentEdit.TextDocument.Uri);
        Assert.Collection(
            textDocumentEdit.Edits,
            AssertTextEdit("Component5", startLine: 2, startCharacter: 1, endLine: 2, endCharacter: 11),
            AssertTextEdit("Component5", startLine: 2, startCharacter: 14, endLine: 2, endCharacter: 24));
    }

    [Fact]
    public async Task Handle_Rename_OnComponentParameter_ReturnsNull()
    {
        // Arrange
        var (endpoint, documentContextFactory) = await CreateEndpointAndDocumentContextFactoryAsync();
        var uri = GetFileUri(ComponentWithParamFilePath);
        var request = new RenameParams
        {
            TextDocument = new() { Uri = uri },
            Position = new Position(1, 14),
            NewName = "Test2"
        };

        var documentContext = await GetDocumentContextAsync(documentContextFactory, uri);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_Rename_OnOpeningBrace_ReturnsNull()
    {
        // Arrange
        var (endpoint, documentContextFactory) = await CreateEndpointAndDocumentContextFactoryAsync();
        var uri = GetFileUri(ComponentWithParamFilePath);
        var request = new RenameParams
        {
            TextDocument = new() { Uri = uri },
            Position = new Position(1, 0),
            NewName = "Test2"
        };

        var documentContext = await GetDocumentContextAsync(documentContextFactory, uri);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_Rename_OnComponentNameLeadingEdge_ReturnsResult()
    {
        // Arrange
        var (endpoint, documentContextFactory) = await CreateEndpointAndDocumentContextFactoryAsync();
        var uri = GetFileUri(ComponentWithParamFilePath);
        var request = new RenameParams
        {
            TextDocument = new() { Uri = uri },
            Position = new Position(1, 1),
            NewName = "Test2"
        };

        var documentContext = await GetDocumentContextAsync(documentContextFactory, uri);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_Rename_OnComponentName_ReturnsResult()
    {
        // Arrange
        var (endpoint, documentContextFactory) = await CreateEndpointAndDocumentContextFactoryAsync();
        var uri = GetFileUri(ComponentWithParamFilePath);
        var request = new RenameParams
        {
            TextDocument = new() { Uri = uri },
            Position = new Position(1, 3),
            NewName = "Test2"
        };

        var documentContext = await GetDocumentContextAsync(documentContextFactory, uri);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Handle_Rename_OnComponentNameTrailingEdge_ReturnsResult()
    {
        // Arrange
        var (endpoint, documentContextFactory) = await CreateEndpointAndDocumentContextFactoryAsync();
        var uri = new Uri(ComponentWithParamFilePath);
        var request = new RenameParams
        {
            TextDocument = new() { Uri = uri },
            Position = new Position(1, 10),
            NewName = "Test2"
        };

        var documentContext = await GetDocumentContextAsync(documentContextFactory, uri);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(result);
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8541")]
    public async Task Handle_Rename_ComponentInSameFile()
    {
        // Arrange
        var (endpoint, documentContextFactory) = await CreateEndpointAndDocumentContextFactoryAsync();
        var uri = GetFileUri(ComponentFilePath4);
        var request = new RenameParams
        {
            TextDocument = new() { Uri = uri },
            Position = new Position(1, 1),
            NewName = "Component5"
        };

        var documentContext = await GetDocumentContextAsync(documentContextFactory, uri);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(result);
        var documentChanges = result.DocumentChanges.AssumeNotNull();
        Assert.Equal(5, documentChanges.Count());

        // We renamed Component3 to Component5, so we should expect file rename.
        var renameChange = documentChanges.ElementAt(0);
        Assert.True(renameChange.TryGetThird(out var renameFile));
        Assert.Equal(GetFileUri(ComponentFilePath3), renameFile.OldUri);
        Assert.Equal(GetFileUri(ComponentFilePath5), renameFile.NewUri);

        var editChange1 = documentChanges.ElementAt(1);
        Assert.True(editChange1.TryGetFirst(out var textDocumentEdit1));
        Assert.Equal(GetFileUri(ComponentFilePath4), textDocumentEdit1.TextDocument.Uri);
        Assert.Equal(2, textDocumentEdit1.Edits.Length);

        var editChange2 = documentChanges.ElementAt(2);
        Assert.True(editChange2.TryGetFirst(out var textDocumentEdit2));
        Assert.Equal(GetFileUri(ComponentFilePath4), textDocumentEdit2.TextDocument.Uri);
        Assert.Equal(2, textDocumentEdit2.Edits.Length);

        var editChange3 = documentChanges.ElementAt(3);
        Assert.True(editChange3.TryGetFirst(out var textDocumentEdit3));
        Assert.Equal(GetFileUri(ComponentFilePath5), textDocumentEdit3.TextDocument.Uri);
        Assert.Collection(
            textDocumentEdit3.Edits,
            AssertTextEdit("Component5", startLine: 1, startCharacter: 1, endLine: 1, endCharacter: 11),
            AssertTextEdit("Component5", startLine: 1, startCharacter: 14, endLine: 1, endCharacter: 24));

        var editChange4 = documentChanges.ElementAt(4);
        Assert.True(editChange4.TryGetFirst(out var textDocumentEdit4));
        Assert.Equal(GetFileUri(ComponentWithParamFilePath), textDocumentEdit4.TextDocument.Uri);
        Assert.Collection(
            textDocumentEdit4.Edits,
            AssertTextEdit("Component5", startLine: 1, startCharacter: 1, endLine: 1, endCharacter: 11),
            AssertTextEdit("Component5", startLine: 1, startCharacter: 32, endLine: 1, endCharacter: 42));
    }

    [Fact]
    public async Task Handle_Rename_FullyQualifiedAndNot()
    {
        // Arrange
        var (endpoint, documentContextFactory) = await CreateEndpointAndDocumentContextFactoryAsync();
        var uri = GetFileUri(IndexFilePath1);
        var request = new RenameParams
        {
            TextDocument = new() { Uri = uri },
            Position = new Position(2, 1),
            NewName = "Component5"
        };

        var documentContext = await GetDocumentContextAsync(documentContextFactory, uri);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(result);
        var documentChanges = result.DocumentChanges.AssumeNotNull();
        Assert.Equal(3, documentChanges.Count());

        var renameChange = documentChanges.ElementAt(0);
        Assert.True(renameChange.TryGetThird(out var renameFile));
        Assert.Equal(GetFileUri(ComponentFilePath1337), renameFile.OldUri);
        Assert.Equal(GetFileUri("c:/First/Component5.razor"), renameFile.NewUri);

        var editChange1 = documentChanges.ElementAt(1);
        Assert.True(editChange1.TryGetFirst(out var textDocumentEdit));
        Assert.Equal(GetFileUri(IndexFilePath1), textDocumentEdit.TextDocument.Uri);
        Assert.Collection(
            textDocumentEdit.Edits,
            AssertTextEdit("Component5", startLine: 2, startCharacter: 1, endLine: 2, endCharacter: 14),
            AssertTextEdit("Component5", startLine: 2, startCharacter: 17, endLine: 2, endCharacter: 30));

        var editChange2 = result.DocumentChanges.Value.ElementAt(2);
        Assert.True(editChange2.TryGetFirst(out var textDocumentEdit2));
        Assert.Equal(GetFileUri(IndexFilePath1), textDocumentEdit2.TextDocument.Uri);
        Assert.Collection(
            textDocumentEdit2.Edits,
            AssertTextEdit("Test.Component5", startLine: 3, startCharacter: 1, endLine: 3, endCharacter: 19),
            AssertTextEdit("Test.Component5", startLine: 3, startCharacter: 22, endLine: 3, endCharacter: 40));
    }

    [Fact]
    public async Task Handle_Rename_MultipleFileUsages()
    {
        // Arrange
        var (endpoint, documentContextFactory) = await CreateEndpointAndDocumentContextFactoryAsync();
        var uri = GetFileUri(ComponentFilePath3);
        var request = new RenameParams
        {
            TextDocument = new() { Uri = uri },
            Position = new Position(1, 1),
            NewName = "Component5"
        };

        var documentContext = await GetDocumentContextAsync(documentContextFactory, uri);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(result);
        var documentChanges = result.DocumentChanges.AssumeNotNull();
        Assert.Equal(5, documentChanges.Count());

        var renameChange = documentChanges.ElementAt(0);
        Assert.True(renameChange.TryGetThird(out var renameFile));
        Assert.Equal(GetFileUri(ComponentFilePath3), renameFile.OldUri);
        Assert.Equal(GetFileUri(ComponentFilePath5), renameFile.NewUri);

        var editChange1 = documentChanges.ElementAt(1);
        Assert.True(editChange1.TryGetFirst(out var textDocumentEdit));
        Assert.Equal(GetFileUri(ComponentFilePath5), textDocumentEdit.TextDocument.Uri);
        Assert.Collection(
            textDocumentEdit.Edits,
            AssertTextEdit("Component5", 1, 1, 1, 11),
            AssertTextEdit("Component5", 1, 14, 1, 24));

        var editChange2 = documentChanges.ElementAt(2);
        Assert.True(editChange2.TryGetFirst(out var textDocumentEdit2));
        Assert.Equal(GetFileUri(ComponentFilePath4), textDocumentEdit2.TextDocument.Uri);
        Assert.Equal(2, textDocumentEdit2.Edits.Length);

        var editChange3 = documentChanges.ElementAt(3);
        Assert.True(editChange3.TryGetFirst(out var textDocumentEdit3));
        Assert.Equal(GetFileUri(ComponentFilePath4), textDocumentEdit3.TextDocument.Uri);
        Assert.Equal(2, textDocumentEdit3.Edits.Length);

        var editChange4 = documentChanges.ElementAt(4);
        Assert.True(editChange4.TryGetFirst(out var textDocumentEdit4));
        Assert.Equal(GetFileUri(ComponentWithParamFilePath), textDocumentEdit4.TextDocument.Uri);
        Assert.Equal(2, textDocumentEdit4.Edits.Length);
    }

    [Fact]
    public async Task Handle_Rename_DifferentDirectories()
    {
        // Arrange
        var (endpoint, documentContextFactory) = await CreateEndpointAndDocumentContextFactoryAsync();
        var uri = GetFileUri(DirectoryFilePath1);
        var request = new RenameParams
        {
            TextDocument = new() { Uri = uri },
            Position = new Position(1, 1),
            NewName = "TestComponent"
        };

        var documentContext = await GetDocumentContextAsync(documentContextFactory, uri);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(result);
        var documentChanges = result.DocumentChanges.AssumeNotNull();
        Assert.Equal(2, documentChanges.Count());

        var renameChange = documentChanges.ElementAt(0);
        Assert.True(renameChange.TryGetThird(out var renameFile));
        Assert.Equal(GetFileUri(DirectoryFilePath2), renameFile.OldUri);
        Assert.Equal(GetFileUri("c:/First/TestComponent.razor"), renameFile.NewUri);

        var editChange = documentChanges.ElementAt(1);
        Assert.True(editChange.TryGetFirst(out var textDocumentEdit));
        Assert.Equal(GetFileUri(DirectoryFilePath1), textDocumentEdit.TextDocument.Uri);
        Assert.Collection(
            textDocumentEdit.Edits,
            AssertTextEdit("TestComponent", startLine: 1, startCharacter: 1, endLine: 1, endCharacter: 11),
            AssertTextEdit("TestComponent", startLine: 1, startCharacter: 14, endLine: 1, endCharacter: 24));
    }

    [Fact]
    public async Task Handle_Rename_SingleServer_CallsDelegatedLanguageServer()
    {
        // Arrange
        var options = StrictMock.Of<LanguageServerFeatureOptions>(o =>
            o.SupportsFileManipulation == true &&
            o.SingleServerSupport == true &&
            o.ReturnCodeActionAndRenamePathsWithPrefixedSlash == false);

        var delegatedEdit = new WorkspaceEdit();

        var clientConnectionMock = new StrictMock<IClientConnection>();
        clientConnectionMock
            .Setup(c => c.SendRequestAsync<IDelegatedParams, WorkspaceEdit>(CustomMessageNames.RazorRenameEndpointName, It.IsAny<DelegatedRenameParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(delegatedEdit);

        var documentMappingServiceMock = new StrictMock<IRazorDocumentMappingService>();
        documentMappingServiceMock
            .Setup(c => c.GetLanguageKind(It.IsAny<RazorCodeDocument>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(RazorLanguageKind.CSharp);
        documentMappingServiceMock
            .Setup(c => c.RemapWorkspaceEditAsync(It.IsAny<WorkspaceEdit>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(delegatedEdit);

        var projectedPosition = new LinePosition(1, 1);
        var projectedIndex = 1;
        documentMappingServiceMock
            .Setup(c => c.TryMapToGeneratedDocumentPosition(It.IsAny<IRazorGeneratedDocument>(), It.IsAny<int>(), out projectedPosition, out projectedIndex))
            .Returns(true);

        var (endpoint, documentContextFactory) = await CreateEndpointAndDocumentContextFactoryAsync(options, documentMappingServiceMock.Object, clientConnectionMock.Object);

        var uri = GetFileUri(ComponentWithParamFilePath);
        var request = new RenameParams
        {
            TextDocument = new() { Uri = uri },
            Position = new Position(1, 0),
            NewName = "Test2"
        };

        var documentContext = await GetDocumentContextAsync(documentContextFactory, uri);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.Same(delegatedEdit, result);
    }

    [Fact]
    public async Task Handle_Rename_SingleServer_DoesntDelegateForRazor()
    {
        // Arrange
        var options = StrictMock.Of<LanguageServerFeatureOptions>(o =>
            o.SupportsFileManipulation == true &&
            o.SingleServerSupport == true &&
            o.ReturnCodeActionAndRenamePathsWithPrefixedSlash == false);

        var clientConnection = StrictMock.Of<IClientConnection>();
        var documentMappingServiceMock = new StrictMock<IRazorDocumentMappingService>();
        documentMappingServiceMock
            .Setup(c => c.GetLanguageKind(It.IsAny<RazorCodeDocument>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(RazorLanguageKind.Razor);

        var (endpoint, documentContextFactory) = await CreateEndpointAndDocumentContextFactoryAsync(options, documentMappingServiceMock.Object, clientConnection);

        var request = new RenameParams
        {
            TextDocument = new() { Uri = GetFileUri(ComponentWithParamFilePath) },
            Position = new Position(1, 0),
            NewName = "Test2"
        };

        var documentContext = await GetDocumentContextAsync(documentContextFactory, request.TextDocument.Uri);
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(request, requestContext, DisposalToken);

        // Assert
        Assert.Null(result);
    }

    private Task<VersionedDocumentContext?> GetDocumentContextAsync(IDocumentContextFactory documentContextFactory, Uri file)
        => RunOnDispatcherAsync(() =>
        {
            return documentContextFactory.TryCreateForOpenDocument(file);
        });

    private async Task<(RenameEndpoint, IDocumentContextFactory)> CreateEndpointAndDocumentContextFactoryAsync(
        LanguageServerFeatureOptions? options = null,
        IRazorDocumentMappingService? documentMappingService = null,
        IClientConnection? clientConnection = null)
    {
        using PooledArrayBuilder<TagHelperDescriptor> builder = [];
        builder.AddRange(CreateRazorComponentTagHelperDescriptors("First", RootNamespace1, "Component1"));
        builder.AddRange(CreateRazorComponentTagHelperDescriptors("First", "Test", "Component2"));
        builder.AddRange(CreateRazorComponentTagHelperDescriptors("Second", RootNamespace2, "Component3"));
        builder.AddRange(CreateRazorComponentTagHelperDescriptors("Second", RootNamespace2, "Component4"));
        builder.AddRange(CreateRazorComponentTagHelperDescriptors("First", "Test", "Component1337"));
        builder.AddRange(CreateRazorComponentTagHelperDescriptors("First", "Test.Components", "Directory1"));
        builder.AddRange(CreateRazorComponentTagHelperDescriptors("First", "Test.Components", "Directory2"));
        var tagHelpers = builder.ToImmutable();

        var projectManager = CreateProjectSnapshotManager();

        var snapshotResolver = new SnapshotResolver(projectManager, LoggerFactory);
        var documentVersionCache = new DocumentVersionCache(projectManager);

        var documentContextFactory = new DocumentContextFactory(projectManager, snapshotResolver, documentVersionCache, LoggerFactory);

        var remoteTextLoaderFactoryMock = new StrictMock<RemoteTextLoaderFactory>();
        remoteTextLoaderFactoryMock
            .Setup(x => x.Create(It.IsAny<string>()))
            .Returns((string filePath) =>
            {
                var textLoaderMock = new StrictMock<TextLoader>();
                textLoaderMock
                    .Setup(x => x.LoadTextAndVersionAsync(It.IsAny<LoadTextOptions>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(TextAndVersion.Create(SourceText.From(""), VersionStamp.Create(), filePath));

                return textLoaderMock.Object;
            });

        var projectService = new RazorProjectService(
            Dispatcher,
            remoteTextLoaderFactoryMock.Object,
            snapshotResolver,
            documentVersionCache,
            projectManager,
            LoggerFactory);

        await RunOnDispatcherAsync(() =>
        {
            var projectKey1 = projectService.AddProject(ProjectFilePath1, IntermediateOutputPath1, RazorConfiguration.Default, RootNamespace1);
            projectManager.ProjectWorkspaceStateChanged(projectKey1, ProjectWorkspaceState.Create(tagHelpers));

            projectService.AddDocument(ComponentFilePath1);
            projectService.AddDocument(ComponentFilePath2);
            projectService.AddDocument(DirectoryFilePath1);
            projectService.AddDocument(DirectoryFilePath2);
            projectService.AddDocument(ComponentFilePath1337);
            projectService.AddDocument(IndexFilePath1);

            projectService.UpdateDocument(ComponentFilePath1, SourceText.From(ComponentText1), version: 42);
            projectService.UpdateDocument(ComponentFilePath2, SourceText.From(ComponentText2), version: 42);
            projectService.UpdateDocument(DirectoryFilePath1, SourceText.From(DirectoryText1), version: 42);
            projectService.UpdateDocument(DirectoryFilePath2, SourceText.From(DirectoryText2), version: 4);
            projectService.UpdateDocument(ComponentFilePath1337, SourceText.From(ComponentText1337), version: 42);
            projectService.UpdateDocument(IndexFilePath1, SourceText.From(IndexText1), version: 42);

            var projectKey2 = projectService.AddProject(ProjectFilePath2, IntermediateOutputPath2, RazorConfiguration.Default, RootNamespace2);
            projectManager.ProjectWorkspaceStateChanged(projectKey2, ProjectWorkspaceState.Create(tagHelpers));

            projectService.AddDocument(ComponentFilePath3);
            projectService.AddDocument(ComponentFilePath4);
            projectService.AddDocument(ComponentWithParamFilePath);

            projectService.UpdateDocument(ComponentFilePath3, SourceText.From(ComponentText3), version: 42);
            projectService.UpdateDocument(ComponentFilePath4, SourceText.From(ComponentText4), version: 42);
            projectService.UpdateDocument(ComponentWithParamFilePath, SourceText.From(ComponentWithParamText), version: 42);
        });

        var searchEngine = new DefaultRazorComponentSearchEngine(projectManager, LoggerFactory);
        options ??= StrictMock.Of<LanguageServerFeatureOptions>(o =>
            o.SupportsFileManipulation == true &&
            o.SingleServerSupport == false &&
            o.ReturnCodeActionAndRenamePathsWithPrefixedSlash == false);

        var documentMappingServiceMock = new StrictMock<IRazorDocumentMappingService>();
        documentMappingServiceMock
            .Setup(c => c.GetLanguageKind(It.IsAny<RazorCodeDocument>(), It.IsAny<int>(), It.IsAny<bool>()))
            .Returns(RazorLanguageKind.Html);
        var projectedPosition = new LinePosition(1, 1);
        var projectedIndex = 1;
        documentMappingServiceMock
            .Setup(c => c.TryMapToGeneratedDocumentPosition(It.IsAny<IRazorGeneratedDocument>(), It.IsAny<int>(), out projectedPosition, out projectedIndex))
            .Returns(true);
        documentMappingService ??= documentMappingServiceMock.Object;

        clientConnection ??= StrictMock.Of<IClientConnection>();

        var endpoint = new RenameEndpoint(
            Dispatcher,
            searchEngine,
            projectManager,
            options,
            documentMappingService,
            clientConnection,
            LoggerFactory);

        return (endpoint, documentContextFactory);
    }

    private static IEnumerable<TagHelperDescriptor> CreateRazorComponentTagHelperDescriptors(string assemblyName, string namespaceName, string tagName)
    {
        var fullyQualifiedName = $"{namespaceName}.{tagName}";
        var builder = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, fullyQualifiedName, assemblyName);
        builder.TagMatchingRule(rule => rule.TagName = tagName);
        builder.SetMetadata(
            TypeName(fullyQualifiedName),
            TypeNameIdentifier(tagName),
            TypeNamespace(namespaceName));

        yield return builder.Build();

        var fullyQualifiedBuilder = TagHelperDescriptorBuilder.Create(ComponentMetadata.Component.TagHelperKind, fullyQualifiedName, assemblyName);
        fullyQualifiedBuilder.TagMatchingRule(rule => rule.TagName = fullyQualifiedName);
        fullyQualifiedBuilder.SetMetadata(
            TypeName(fullyQualifiedName),
            TypeNameIdentifier(tagName),
            TypeNamespace(namespaceName),
            new(ComponentMetadata.Component.NameMatchKey, ComponentMetadata.Component.FullyQualifiedNameMatch));

        yield return fullyQualifiedBuilder.Build();
    }

    private static Action<TextEdit> AssertTextEdit(string fileName, int startLine, int startCharacter, int endLine, int endCharacter)
        => edit =>
        {
            Assert.Equal(fileName, edit.NewText);

            var range = edit.Range;
            Assert.Equal(startLine, range.Start.Line);
            Assert.Equal(startCharacter, range.Start.Character);
            Assert.Equal(endLine, range.End.Line);
            Assert.Equal(endCharacter, range.End.Character);
        };
}
