﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Editor.Razor.Logging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Test;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

public class RazorCustomMessageTargetTest : TestBase
{
    private readonly ITextBuffer _textBuffer;
    private readonly IClientSettingsManager _editorSettingsManager;

    public RazorCustomMessageTargetTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _textBuffer = new TestTextBuffer(new StringTextSnapshot(string.Empty));
        _editorSettingsManager = new ClientSettingsManager(Array.Empty<ClientSettingsChangedTrigger>());
    }

    [Fact]
    public async Task UpdateCSharpBuffer_CannotLookupDocument_NoopsGracefully()
    {
        // Arrange
        LSPDocumentSnapshot document;
        var documentManager = new Mock<TrackingLSPDocumentManager>(MockBehavior.Strict);
        documentManager
            .Setup(manager => manager.TryGetDocument(It.IsAny<Uri>(), out document))
            .Returns(false);
        var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);
        var outputWindowLogger = new TestOutputWindowLogger();

        var target = new RazorCustomMessageTarget(
            documentManager.Object,
            JoinableTaskContext,
            Mock.Of<LSPRequestInvoker>(MockBehavior.Strict),
            TestFormattingOptionsProvider.Default,
            _editorSettingsManager,
            documentSynchronizer.Object,
            new CSharpVirtualDocumentAddListener(outputWindowLogger),
            Mock.Of<ITelemetryReporter>(MockBehavior.Strict),
            TestLanguageServerFeatureOptions.Instance,
            Mock.Of<ProjectSnapshotManagerAccessor>(MockBehavior.Strict));
        var request = new UpdateBufferRequest()
        {
            HostDocumentFilePath = "C:/path/to/file.razor",
            Changes = null
        };

        // Act & Assert
        await target.UpdateCSharpBufferCoreAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task UpdateCSharpBuffer_UpdatesDocument()
    {
        // Arrange
        var doc1 = new CSharpVirtualDocumentSnapshot(projectKey: default, new Uri("C:/path/to/file.razor.g.cs"), _textBuffer.CurrentSnapshot, 0);
        var documents = new[] { doc1 };
        var document = Mock.Of<LSPDocumentSnapshot>(d => d.VirtualDocuments == documents, MockBehavior.Strict);
        var documentManager = new Mock<TrackingLSPDocumentManager>(MockBehavior.Strict);
        documentManager
            .Setup(manager => manager.UpdateVirtualDocument<CSharpVirtualDocument>(
                It.IsAny<Uri>(),
                It.IsAny<IReadOnlyList<ITextChange>>(),
                1337,
                It.IsAny<object>()))
            .Verifiable();
        var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);

        var outputWindowLogger = new TestOutputWindowLogger();

        var target = new RazorCustomMessageTarget(
            documentManager.Object,
            JoinableTaskContext,
            Mock.Of<LSPRequestInvoker>(MockBehavior.Strict),
            TestFormattingOptionsProvider.Default,
            _editorSettingsManager,
            documentSynchronizer.Object,
            new CSharpVirtualDocumentAddListener(outputWindowLogger),
            Mock.Of<ITelemetryReporter>(MockBehavior.Strict),
            TestLanguageServerFeatureOptions.Instance,
            Mock.Of<ProjectSnapshotManagerAccessor>(MockBehavior.Strict));
        var request = new UpdateBufferRequest()
        {
            HostDocumentFilePath = "C:/path/to/file.razor",
            HostDocumentVersion = 1337,
            Changes = Array.Empty<TextChange>(),
        };

        // Act
        await target.UpdateCSharpBufferCoreAsync(request, CancellationToken.None);

        // Assert
        documentManager.VerifyAll();
    }

    [Fact]
    public async Task UpdateCSharpBuffer_UpdatesCorrectDocument()
    {
        // Arrange
        var projectKey1 = TestProjectKey.Create("Project1");
        var projectKey2 = TestProjectKey.Create("Project2");
        var doc1 = new CSharpVirtualDocumentSnapshot(projectKey1, new Uri("C:/path/to/p1/file.razor.g.cs"), _textBuffer.CurrentSnapshot, 0);
        var doc2 = new CSharpVirtualDocumentSnapshot(projectKey2, new Uri("C:/path/to/p2/file.razor.g.cs"), _textBuffer.CurrentSnapshot, 0);
        var documents = new[] { doc1, doc2 };
        var document = Mock.Of<LSPDocumentSnapshot>(d => d.VirtualDocuments == documents, MockBehavior.Strict);
        var documentManager = new Mock<TrackingLSPDocumentManager>(MockBehavior.Strict);
        documentManager
          .Setup(manager => manager.TryGetDocument(It.IsAny<Uri>(), out document))
          .Returns(true);
        documentManager
            .Setup(manager => manager.UpdateVirtualDocument<CSharpVirtualDocument>(
                It.IsAny<Uri>(),
                doc2.Uri,
                It.IsAny<IReadOnlyList<ITextChange>>(),
                1337,
                It.IsAny<object>()))
            .Verifiable();
        var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);

        var outputWindowLogger = new TestOutputWindowLogger();

        var target = new RazorCustomMessageTarget(
            documentManager.Object,
            JoinableTaskContext,
            Mock.Of<LSPRequestInvoker>(MockBehavior.Strict),
            TestFormattingOptionsProvider.Default,
            _editorSettingsManager,
            documentSynchronizer.Object,
            new CSharpVirtualDocumentAddListener(outputWindowLogger),
            Mock.Of<ITelemetryReporter>(MockBehavior.Strict),
            new TestLanguageServerFeatureOptions(includeProjectKeyInGeneratedFilePath: true),
            Mock.Of<ProjectSnapshotManagerAccessor>(MockBehavior.Strict));
        var request = new UpdateBufferRequest()
        {
            ProjectKeyId = projectKey2.Id,
            HostDocumentFilePath = "C:/path/to/file.razor",
            HostDocumentVersion = 1337,
            Changes = Array.Empty<TextChange>(),
        };

        // Act
        await target.UpdateCSharpBufferCoreAsync(request, CancellationToken.None);

        // Assert
        documentManager.VerifyAll();
    }

    [Fact]
    public async Task ProvideCodeActionsAsync_CannotLookupDocument_ReturnsNullAsync()
    {
        // Arrange
        LSPDocumentSnapshot document;
        var documentManager = new Mock<TrackingLSPDocumentManager>(MockBehavior.Strict);
        documentManager
            .Setup(manager => manager.TryGetDocument(It.IsAny<Uri>(), out document))
            .Returns(false);
        var documentSynchronizer = GetDocumentSynchronizer();
        var outputWindowLogger = new TestOutputWindowLogger();

        var target = new RazorCustomMessageTarget(
            documentManager.Object,
            JoinableTaskContext,
            Mock.Of<LSPRequestInvoker>(MockBehavior.Strict),
            TestFormattingOptionsProvider.Default,
            _editorSettingsManager,
            documentSynchronizer,
            new CSharpVirtualDocumentAddListener(outputWindowLogger),
            Mock.Of<ITelemetryReporter>(MockBehavior.Strict),
            TestLanguageServerFeatureOptions.Instance,
            Mock.Of<ProjectSnapshotManagerAccessor>(MockBehavior.Strict));
        var request = new DelegatedCodeActionParams()
        {
            HostDocumentVersion = 1,
            LanguageKind = RazorLanguageKind.CSharp,
            CodeActionParams = new VSCodeActionParams()
            {
                TextDocument = new VSTextDocumentIdentifier()
                {
                    Uri = new Uri("C:/path/to/file.razor")
                },
                Range = new Range(),
                Context = new VSInternalCodeActionContext()
            }
        };

        // Act
        var result = await target.ProvideCodeActionsAsync(request, DisposalToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ProvideCodeActionsAsync_ReturnsCodeActionsAsync()
    {
        // Arrange
        var testDocUri = new Uri("C:/path/to/file.razor");
        var testVirtualDocUri = new Uri("C:/path/to/file2.razor.g");
        var testCSharpDocUri = new Uri("C:/path/to/file.razor.g.cs");

        var testVirtualDocument = new TestVirtualDocumentSnapshot(testVirtualDocUri, 0);
        var csharpVirtualDocument = new CSharpVirtualDocumentSnapshot(projectKey: default, testCSharpDocUri, _textBuffer.CurrentSnapshot, 0);
        LSPDocumentSnapshot testDocument = new TestLSPDocumentSnapshot(testDocUri, 0, testVirtualDocument, csharpVirtualDocument);

        var documentManager = new Mock<TrackingLSPDocumentManager>(MockBehavior.Strict);
        documentManager
            .Setup(manager => manager.TryGetDocument(It.IsAny<Uri>(), out testDocument))
            .Returns(true);

        var languageServer1Response = new[] { new VSInternalCodeAction() { Title = "Response 1" } };
        var languageServer2Response = new[] { new VSInternalCodeAction() { Title = "Response 2" } };

        async IAsyncEnumerable<ReinvocationResponse<IReadOnlyList<VSInternalCodeAction>>> GetExpectedResultsAsync()
        {
            yield return new ReinvocationResponse<IReadOnlyList<VSInternalCodeAction>>("languageClient", languageServer1Response);
            yield return new ReinvocationResponse<IReadOnlyList<VSInternalCodeAction>>("languageClient", languageServer2Response);

            await Task.CompletedTask;
        }

        var expectedResults = GetExpectedResultsAsync();
        var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
        requestInvoker
            .Setup(invoker => invoker.ReinvokeRequestOnMultipleServersAsync<VSCodeActionParams, IReadOnlyList<VSInternalCodeAction>>(
                _textBuffer,
                Methods.TextDocumentCodeActionName,
                It.IsAny<Func<JToken, bool>>(),
                It.IsAny<VSCodeActionParams>(),
                It.IsAny<CancellationToken>()))
            .Returns(expectedResults);

        var documentSynchronizer = GetDocumentSynchronizer(GetCSharpSnapshot());
        var outputWindowLogger = new TestOutputWindowLogger();
        var telemetryReporter = new Mock<ITelemetryReporter>(MockBehavior.Strict);
        telemetryReporter.Setup(r => r.TrackLspRequest(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>())).Returns(NullScope.Instance);
        var csharpVirtualDocumentAddListener = new CSharpVirtualDocumentAddListener(outputWindowLogger);

        var target = new RazorCustomMessageTarget(
                documentManager.Object, JoinableTaskContext, requestInvoker.Object,
                TestFormattingOptionsProvider.Default, _editorSettingsManager, documentSynchronizer, csharpVirtualDocumentAddListener, telemetryReporter.Object, TestLanguageServerFeatureOptions.Instance, Mock.Of<ProjectSnapshotManagerAccessor>(MockBehavior.Strict));

        var request = new DelegatedCodeActionParams()
        {
            HostDocumentVersion = 1,
            LanguageKind = RazorLanguageKind.CSharp,
            CodeActionParams = new VSCodeActionParams()
            {
                TextDocument = new VSTextDocumentIdentifier()
                {
                    Uri = testDocUri
                },
                Range = new Range(),
                Context = new VSInternalCodeActionContext()
            }
        };

        // Act
        var result = await target.ProvideCodeActionsAsync(request, DisposalToken);

        // Assert
        Assert.Collection(result,
            r => Assert.Equal(languageServer1Response[0].Title, r.Title),
            r => Assert.Equal(languageServer2Response[0].Title, r.Title));
    }

    [Fact]
    public async Task ResolveCodeActionsAsync_ReturnsSingleCodeAction()
    {
        // Arrange
        var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
        var csharpVirtualDocument = new CSharpVirtualDocumentSnapshot(projectKey: default, new Uri("C:/path/to/file.razor.g.cs"), _textBuffer.CurrentSnapshot, hostDocumentSyncVersion: 0);
        var documentManager = new TestDocumentManager();
        var razorUri = new Uri("C:/path/to/file.razor");
        documentManager.AddDocument(razorUri, new TestLSPDocumentSnapshot(razorUri, version: 0, "Some Content", csharpVirtualDocument));
        var expectedCodeAction = new VSInternalCodeAction()
        {
            Title = "Something",
            Data = new object()
        };
        var unexpectedCodeAction = new VSInternalCodeAction()
        {
            Title = "Something Else",
            Data = new object()
        };

        async IAsyncEnumerable<ReinvocationResponse<VSInternalCodeAction>> GetExpectedResultsAsync()
        {
            yield return new ReinvocationResponse<VSInternalCodeAction>("languageClient", expectedCodeAction);
            yield return new ReinvocationResponse<VSInternalCodeAction>("languageClient", unexpectedCodeAction);

            await Task.CompletedTask;
        }

        var expectedResponses = GetExpectedResultsAsync();
        requestInvoker
            .Setup(invoker => invoker.ReinvokeRequestOnMultipleServersAsync<CodeAction, VSInternalCodeAction>(
                It.IsAny<ITextBuffer>(),
                Methods.CodeActionResolveName,
                It.IsAny<Func<JToken, bool>>(),
                It.IsAny<VSInternalCodeAction>(),
                It.IsAny<CancellationToken>()))
            .Returns(expectedResponses);

        var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);
        documentSynchronizer
            .Setup(r => r.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                1,
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DefaultLSPDocumentSynchronizer.SynchronizedResult<CSharpVirtualDocumentSnapshot>(true, csharpVirtualDocument));
        var outputWindowLogger = new TestOutputWindowLogger();
        var telemetryReporter = new Mock<ITelemetryReporter>(MockBehavior.Strict);
        var csharpVirtualDocumentAddListener = new CSharpVirtualDocumentAddListener(outputWindowLogger);

        var target = new RazorCustomMessageTarget(
            documentManager, JoinableTaskContext, requestInvoker.Object,
            TestFormattingOptionsProvider.Default, _editorSettingsManager, documentSynchronizer.Object, csharpVirtualDocumentAddListener, telemetryReporter.Object, TestLanguageServerFeatureOptions.Instance, Mock.Of<ProjectSnapshotManagerAccessor>(MockBehavior.Strict));

        var codeAction = new VSInternalCodeAction()
        {
            Title = "Something",
        };
        var request = new RazorResolveCodeActionParams(razorUri, HostDocumentVersion: 1, RazorLanguageKind.CSharp, codeAction);

        // Act
        var result = await target.ResolveCodeActionsAsync(request, DisposalToken);

        // Assert
        Assert.Equal(expectedCodeAction.Title, result.Title);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProvideSemanticTokensAsync_CannotLookupDocument_ReturnsNullAsync(bool isPreciseRange)
    {
        // Arrange
        LSPDocumentSnapshot document;
        var documentManager = new Mock<TrackingLSPDocumentManager>(MockBehavior.Strict);
        documentManager
            .Setup(manager => manager.TryGetDocument(It.IsAny<Uri>(), out document))
            .Returns(false);
        var documentSynchronizer = GetDocumentSynchronizer();
        var outputWindowLogger = new TestOutputWindowLogger();

        var target = new RazorCustomMessageTarget(
            documentManager.Object,
            JoinableTaskContext,
            Mock.Of<LSPRequestInvoker>(MockBehavior.Strict),
            TestFormattingOptionsProvider.Default,
            _editorSettingsManager,
            documentSynchronizer,
            new CSharpVirtualDocumentAddListener(outputWindowLogger),
            Mock.Of<ITelemetryReporter>(MockBehavior.Strict),
            TestLanguageServerFeatureOptions.Instance,
            Mock.Of<ProjectSnapshotManagerAccessor>(MockBehavior.Strict));
        var request = new ProvideSemanticTokensRangesParams(
            textDocument: new TextDocumentIdentifier()
            {
                Uri = new Uri("C:/path/to/file.razor")
            },
            requiredHostDocumentVersion: 1,
            ranges: new[] { new Range() },
            correlationId: Guid.Empty);

        // Act
        var result = isPreciseRange?
            await target.ProvidePreciseRangeSemanticTokensAsync(request, DisposalToken):
            await target.ProvideMinimalRangeSemanticTokensAsync(request, DisposalToken);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProvideSemanticTokensAsync_CannotLookupVirtualDocument_ReturnsNullAsync(bool isPreciseRange)
    {
        // Arrange
        var testDocUri = new Uri("C:/path/to/file.razor");
        LSPDocumentSnapshot testDocument = new TestLSPDocumentSnapshot(testDocUri, 0);

        var documentManager = new Mock<TrackingLSPDocumentManager>(MockBehavior.Strict);
        documentManager
            .Setup(manager => manager.TryGetDocument(It.IsAny<Uri>(), out testDocument))
            .Returns(true);
        var documentSynchronizer = GetDocumentSynchronizer();
        var outputWindowLogger = new TestOutputWindowLogger();

        var target = new RazorCustomMessageTarget(
            documentManager.Object,
            JoinableTaskContext,
            Mock.Of<LSPRequestInvoker>(MockBehavior.Strict),
            TestFormattingOptionsProvider.Default,
            _editorSettingsManager,
            documentSynchronizer,
            new CSharpVirtualDocumentAddListener(outputWindowLogger),
            Mock.Of<ITelemetryReporter>(MockBehavior.Strict),
            TestLanguageServerFeatureOptions.Instance,
            Mock.Of<ProjectSnapshotManagerAccessor>(MockBehavior.Strict));
        var request = new ProvideSemanticTokensRangesParams(
            textDocument: new TextDocumentIdentifier()
            {
                Uri = new Uri("C:/path/to/file.razor")
            },
            requiredHostDocumentVersion: 0,
            ranges: new[] { new Range() },
            correlationId: Guid.Empty);

        // Act
        var result = isPreciseRange ?
            await target.ProvidePreciseRangeSemanticTokensAsync(request, DisposalToken) :
            await target.ProvideMinimalRangeSemanticTokensAsync(request, DisposalToken);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProvideSemanticTokensAsync_ContainsRange_ReturnsSemanticTokens(bool isPreciseRange)
    {
        // Arrange
        var testDocUri = new Uri("C:/path/to%20-%20project/file.razor");
        var testVirtualDocUri = new Uri("C:/path/to - project/file2.razor.g");
        var testCSharpDocUri = new Uri("C:/path/to - project/file.razor.g.cs");

        var documentVersion = 0;
        var testVirtualDocument = new TestVirtualDocumentSnapshot(testVirtualDocUri, 0);
        var csharpVirtualDocument = new CSharpVirtualDocumentSnapshot(projectKey: default, testCSharpDocUri, _textBuffer.CurrentSnapshot, 0);
        LSPDocumentSnapshot testDocument = new TestLSPDocumentSnapshot(testDocUri, documentVersion, testVirtualDocument, csharpVirtualDocument);

        var documentManager = new Mock<TrackingLSPDocumentManager>(MockBehavior.Strict);
        documentManager
            .Setup(manager => manager.TryGetDocument(testDocUri, out testDocument))
            .Returns(true);

        var expectedCSharpResults = new VSSemanticTokensResponse() { Data = new int[] { It.IsAny<int>() } };
        var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
        requestInvoker
            .Setup(invoker => invoker.ReinvokeRequestOnServerAsync<SemanticTokensParams, VSSemanticTokensResponse>(
                _textBuffer,
                It.IsAny<string>(),
                RazorLSPConstants.RazorCSharpLanguageServerName,
                It.IsAny<Func<JToken, bool>>(),
                It.IsAny<SemanticTokensParams>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReinvocationResponse<VSSemanticTokensResponse>("languageClient", expectedCSharpResults));

        var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);
        documentSynchronizer
            .Setup(r => r.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                0,
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DefaultLSPDocumentSynchronizer.SynchronizedResult<CSharpVirtualDocumentSnapshot>(true, csharpVirtualDocument));
        var outputWindowLogger = new TestOutputWindowLogger();
        var telemetryReporter = new Mock<ITelemetryReporter>(MockBehavior.Strict);
        telemetryReporter.Setup(r => r.BeginBlock(It.IsAny<string>(), It.IsAny<Severity>(), It.IsAny<ImmutableDictionary<string, object>>())).Returns(NullScope.Instance);
        telemetryReporter.Setup(r => r.TrackLspRequest(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>())).Returns(NullScope.Instance);
        var csharpVirtualDocumentAddListener = new CSharpVirtualDocumentAddListener(outputWindowLogger);

        var target = new RazorCustomMessageTarget(
            documentManager.Object, JoinableTaskContext, requestInvoker.Object,
            TestFormattingOptionsProvider.Default, _editorSettingsManager, documentSynchronizer.Object, csharpVirtualDocumentAddListener, telemetryReporter.Object, TestLanguageServerFeatureOptions.Instance, Mock.Of<ProjectSnapshotManagerAccessor>(MockBehavior.Strict));
        var request = new ProvideSemanticTokensRangesParams(
            textDocument: new TextDocumentIdentifier()
            {
                Uri = new Uri("C:/path/to%20-%20project/file.razor")
            },
            requiredHostDocumentVersion: 0,
            ranges: new[] { new Range() { Start = It.IsAny<Position>(), End = It.IsAny<Position>() } },
            correlationId: Guid.Empty);

        // Act
        var result = isPreciseRange ?
            await target.ProvidePreciseRangeSemanticTokensAsync(request, DisposalToken) :
            await target.ProvideMinimalRangeSemanticTokensAsync(request, DisposalToken);

        // Assert
        Assert.Equal(documentVersion, result.HostDocumentSyncVersion);
        Assert.Equal(expectedCSharpResults.Data, result.Tokens);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProvideSemanticTokensAsync_EmptyRange_ReturnsNoSemanticTokens(bool isPreciseRange)
    {
        // Arrange
        var testDocUri = new Uri("C:/path/to%20-%20project/file.razor");
        var testVirtualDocUri = new Uri("C:/path/to - project/file2.razor.g");
        var testCSharpDocUri = new Uri("C:/path/to - project/file.razor.g.cs");

        var documentVersion = 0;
        var testVirtualDocument = new TestVirtualDocumentSnapshot(testVirtualDocUri, 0);
        var csharpVirtualDocument = new CSharpVirtualDocumentSnapshot(projectKey: default, testCSharpDocUri, _textBuffer.CurrentSnapshot, 0);
        LSPDocumentSnapshot testDocument = new TestLSPDocumentSnapshot(testDocUri, documentVersion, testVirtualDocument, csharpVirtualDocument);

        var documentManager = new Mock<TrackingLSPDocumentManager>(MockBehavior.Strict);
        documentManager
            .Setup(manager => manager.TryGetDocument(testDocUri, out testDocument))
            .Returns(true);

        var expectedCSharpResults = new VSSemanticTokensResponse();
        var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
        requestInvoker
            .Setup(invoker => invoker.ReinvokeRequestOnServerAsync<SemanticTokensParams, VSSemanticTokensResponse>(
                _textBuffer,
                It.IsAny<string>(),
                RazorLSPConstants.RazorCSharpLanguageServerName,
                It.IsAny<Func<JToken, bool>>(),
                It.IsAny<SemanticTokensParams>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReinvocationResponse<VSSemanticTokensResponse>("languageClient", expectedCSharpResults));

        var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);
        documentSynchronizer
            .Setup(r => r.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(
                0,
                It.IsAny<Uri>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DefaultLSPDocumentSynchronizer.SynchronizedResult<CSharpVirtualDocumentSnapshot>(true, csharpVirtualDocument));
        var outputWindowLogger = new TestOutputWindowLogger();
        var telemetryReporter = new Mock<ITelemetryReporter>(MockBehavior.Strict);
        telemetryReporter.Setup(r => r.BeginBlock(It.IsAny<string>(), It.IsAny<Severity>(), It.IsAny<ImmutableDictionary<string, object>>())).Returns(NullScope.Instance);
        telemetryReporter.Setup(r => r.TrackLspRequest(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>())).Returns(NullScope.Instance);
        var csharpVirtualDocumentAddListener = new CSharpVirtualDocumentAddListener(outputWindowLogger);

        var target = new RazorCustomMessageTarget(
            documentManager.Object, JoinableTaskContext, requestInvoker.Object,
            TestFormattingOptionsProvider.Default, _editorSettingsManager, documentSynchronizer.Object, csharpVirtualDocumentAddListener, telemetryReporter.Object, TestLanguageServerFeatureOptions.Instance, Mock.Of<ProjectSnapshotManagerAccessor>(MockBehavior.Strict));
        var request = new ProvideSemanticTokensRangesParams(
            textDocument: new TextDocumentIdentifier()
            {
                Uri = new Uri("C:/path/to%20-%20project/file.razor")
            },
            requiredHostDocumentVersion: 0,
            ranges: new[] { new Range() },
            correlationId: Guid.Empty);
        var expectedResults = new ProvideSemanticTokensResponse(null, documentVersion);

        // Act
        var result = isPreciseRange ?
            await target.ProvidePreciseRangeSemanticTokensAsync(request, DisposalToken) :
            await target.ProvideMinimalRangeSemanticTokensAsync(request, DisposalToken);

        // Assert
        Assert.Equal(documentVersion, result.HostDocumentSyncVersion);
        Assert.Null(result.Tokens);
    }

    private LSPDocumentSynchronizer GetDocumentSynchronizer(CSharpVirtualDocumentSnapshot csharpDoc = null, HtmlVirtualDocumentSnapshot htmlDoc = null)
    {
        var synchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);
        synchronizer.Setup(s => s.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(It.IsAny<int>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DefaultLSPDocumentSynchronizer.SynchronizedResult<CSharpVirtualDocumentSnapshot>(csharpDoc is not null, csharpDoc));

        synchronizer.Setup(s => s.TrySynchronizeVirtualDocumentAsync<CSharpVirtualDocumentSnapshot>(It.IsAny<int>(), It.IsAny<Uri>(), It.IsAny<Uri>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DefaultLSPDocumentSynchronizer.SynchronizedResult<CSharpVirtualDocumentSnapshot>(csharpDoc is not null, csharpDoc));

        synchronizer.Setup(s => s.TrySynchronizeVirtualDocumentAsync<HtmlVirtualDocumentSnapshot>(It.IsAny<int>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DefaultLSPDocumentSynchronizer.SynchronizedResult<HtmlVirtualDocumentSnapshot>(htmlDoc is not null, htmlDoc));

        return synchronizer.Object;
    }

    private CSharpVirtualDocumentSnapshot GetCSharpSnapshot(Uri uri = null, int hostDocumentSyncVersion = 1)
    {
        if (uri is null)
        {
            uri = new Uri("C:/thing.razor");
        }

        var textBuffer = new Mock<ITextBuffer>(MockBehavior.Strict);
        var snapshot = new Mock<ITextSnapshot>(MockBehavior.Strict);
        snapshot.Setup(s => s.TextBuffer)
            .Returns(_textBuffer);

        var csharpDoc = new CSharpVirtualDocumentSnapshot(projectKey: default, uri, snapshot.Object, hostDocumentSyncVersion);

        return csharpDoc;
    }

    private class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new NullScope();
        private NullScope() { }
        public void Dispose() { }
    }

    private class TestOutputWindowLogger : IOutputWindowLogger
    {
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
        }

        public void SetTestLogger(ILogger testOutputLogger)
        {
        }
    }
}
