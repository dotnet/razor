// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class OnTypeFormattingHandlerTest : HandlerTestBase
    {
        public OnTypeFormattingHandlerTest()
        {
            Uri = new Uri("C:/path/to/file.razor");
        }

        private Uri Uri { get; }

        private readonly ILanguageClient _languageClient = Mock.Of<ILanguageClient>(MockBehavior.Strict);

        [Fact]
        public async Task HandleRequest_DocumentNotFound_ReturnsNullAsync()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var projectionProvider = Mock.Of<LSPProjectionProvider>(MockBehavior.Strict);
            var documentSynchronizer = Mock.Of<LSPDocumentSynchronizer>(MockBehavior.Strict);
            var mappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);
            var workspace = TestWorkspace.Create();
            var hostServicesProvider = new VSHostServicesProvider(workspace);
            var formatOnTypeHandler = new OnTypeFormattingHandler(
                documentManager, documentSynchronizer, projectionProvider, mappingProvider, hostServicesProvider, LoggerProvider);
            var formattingRequest = new DocumentOnTypeFormattingParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1),
                Character = ";",
                Options = new FormattingOptions()
            };

            // Act
            var result = await formatOnTypeHandler.HandleRequestAsync(
                formattingRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_ProjectionNotFound_ReturnsNullAsync()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var snapshot = new StringTextSnapshot(@"
@code {
public string _foo;
}");
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(m => m.Snapshot == snapshot, MockBehavior.Strict));
            var documentSynchronizer = Mock.Of<LSPDocumentSynchronizer>(MockBehavior.Strict);
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict).Object;
            Mock.Get(projectionProvider).Setup(projectionProvider => projectionProvider.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), CancellationToken.None))
                .Returns(Task.FromResult<ProjectionResult>(null));
            var mappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);
            var workspace = TestWorkspace.Create();
            var hostServicesProvider = new VSHostServicesProvider(workspace);
            var formatOnTypeHandler = new OnTypeFormattingHandler(
                documentManager, documentSynchronizer, projectionProvider, mappingProvider, hostServicesProvider, LoggerProvider);
            var formattingRequest = new DocumentOnTypeFormattingParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1),
                Character = ";",
                Options = new FormattingOptions()
            };

            // Act
            var result = await formatOnTypeHandler.HandleRequestAsync(formattingRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequest_HtmlProjection_ReturnsNullAsync()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var snapshot = new StringTextSnapshot(@"
@code {
public string _foo;
}");
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(m => m.Snapshot == snapshot, MockBehavior.Strict));
            var documentSynchronizer = Mock.Of<LSPDocumentSynchronizer>(MockBehavior.Strict);
            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));
            var mappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);
            var workspace = TestWorkspace.Create();
            var hostServicesProvider = new VSHostServicesProvider(workspace);
            var formatOnTypeHandler = new OnTypeFormattingHandler(
                documentManager, documentSynchronizer, projectionProvider.Object, mappingProvider, hostServicesProvider, LoggerProvider);
            var formattingRequest = new DocumentOnTypeFormattingParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1),
                Character = ";",
                Options = new FormattingOptions()
            };

            // Act
            var result = await formatOnTypeHandler.HandleRequestAsync(formattingRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_RazorProjection_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var snapshot = new StringTextSnapshot(@"
@code {
public string _foo;
}");
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(m => m.Snapshot == snapshot, MockBehavior.Strict));
            var documentSynchronizer = Mock.Of<LSPDocumentSynchronizer>(MockBehavior.Strict);
            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Razor,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));
            var mappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);
            var workspace = TestWorkspace.Create();
            var hostServicesProvider = new VSHostServicesProvider(workspace);
            var formatOnTypeHandler = new OnTypeFormattingHandler(
                documentManager, documentSynchronizer, projectionProvider.Object, mappingProvider, hostServicesProvider, LoggerProvider);
            var formattingRequest = new DocumentOnTypeFormattingParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1),
                Character = ";",
                Options = new FormattingOptions()
            };

            // Act
            var result = await formatOnTypeHandler.HandleRequestAsync(formattingRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequest_UnexpectedTriggerCharacter_ReturnsNullAsync()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var snapshot = new StringTextSnapshot(@"
@code {
public string _foo;
}");
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(m => m.Snapshot == snapshot, MockBehavior.Strict));
            var documentSynchronizer = Mock.Of<LSPDocumentSynchronizer>(MockBehavior.Strict);
            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));
            var mappingProvider = Mock.Of<LSPDocumentMappingProvider>(MockBehavior.Strict);
            var workspace = TestWorkspace.Create();
            var hostServicesProvider = new VSHostServicesProvider(workspace);
            var formatOnTypeHandler = new OnTypeFormattingHandler(
                documentManager, documentSynchronizer, projectionProvider.Object, mappingProvider, hostServicesProvider, LoggerProvider);
            var formattingRequest = new DocumentOnTypeFormattingParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1),
                Character = ".",
                Options = new FormattingOptions()
            };

            // Act
            var result = await formatOnTypeHandler.HandleRequestAsync(formattingRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequest_CSharpProjection_RemapsResultAsync()
        {
            // Arrange
            var remapped = false;
            var documentManager = new TestDocumentManager();

            // The intent of this test is to simply verify that the C# formatter is being called and
            // that mapping is occurring. Testing of the behavior of the C# formatter itself is done
            // in other tests below and in other test classes.
            var text = @"
class C
{
    void M()
    {
    var x = 1;
    }
}";
            var snapshot = new StringTextSnapshot(text);
            var documentSnapshot = new Mock<LSPDocumentSnapshot>(MockBehavior.Strict);
            documentSnapshot.Setup(s => s.Version).Returns(2);
            documentSnapshot.Setup(s => s.Snapshot).Returns(snapshot);
            var virtualDocuments = new List<VirtualDocumentSnapshot>()
            {
                new CSharpVirtualDocumentSnapshot(Uri, snapshot, hostDocumentSyncVersion: 2)
            };
            documentSnapshot.Setup(s => s.VirtualDocuments).Returns(virtualDocuments);
            documentManager.AddDocument(Uri, documentSnapshot.Object);
            var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);
            documentSynchronizer.Setup(ds => ds.TrySynchronizeVirtualDocumentAsync(
                It.IsAny<int>(), It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
            var linePosition = new LinePosition(5, 14);
            var sourceText = SourceText.From(text);
            var positionIndex = sourceText.Lines.GetPosition(linePosition);
            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
                PositionIndex = positionIndex
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(
                p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));
            var mappingProvider = new Mock<LSPDocumentMappingProvider>(MockBehavior.Strict);
            mappingProvider
                .Setup(m => m.RemapFormattedTextEditsAsync(It.IsAny<Uri>(), It.IsAny<TextEdit[]>(), It.IsAny<FormattingOptions>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback(() => remapped = true)
                .Returns(Task.FromResult(new[] { new TextEdit() }));
            var workspace = TestWorkspace.Create();
            var hostServicesProvider = new VSHostServicesProvider(workspace);

            var formatOnTypeHandler = new OnTypeFormattingHandler(
                documentManager, documentSynchronizer.Object, projectionProvider.Object, mappingProvider.Object,
                hostServicesProvider, LoggerProvider);

            var formattingRequest = new DocumentOnTypeFormattingParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(5, 14),
                Character = ";",
                Options = new FormattingOptions { InsertSpaces = true, TabSize = 4 }
            };

            // Act
            var result = await formatOnTypeHandler.HandleRequestAsync(
                formattingRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(remapped);
        }

        [Fact]
        public async Task RazorCSharpFormattingInteractionService_GetFormattingChanges_ReturnsCorrectTextChangeAsync()
        {
            // Arrange
            var csharpText = @"
class C
{
    void M()
    {
    var x = 1;
    }
}";
            var csharpSourceText = SourceText.From(csharpText);
            var workspace = TestWorkspace.Create();
            var hostServicesProvider = new VSHostServicesProvider(workspace);
            var document = OnTypeFormattingHandler.GenerateRoslynCSharpDocument(csharpSourceText, hostServicesProvider);

            var request = new DocumentOnTypeFormattingParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(5, 14),
                Character = ";",
                Options = new FormattingOptions { InsertSpaces = true, TabSize = 4 }
            };

            var documentOptions = await OnTypeFormattingHandler.GetDocumentOptionsAsync(request, document).ConfigureAwait(false);
            var linePosition = new LinePosition(request.Position.Line, request.Position.Character);
            var positionIndex = csharpSourceText.Lines.GetPosition(linePosition);

            var expectedTextChange = new TextChange(new TextSpan(39, 0), "    ");

            // Act
            var formattingChanges = await RazorCSharpFormattingInteractionService.GetFormattingChangesAsync(
                document, typedChar: request.Character[0], positionIndex, documentOptions, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Single(formattingChanges);
            Assert.Equal(expectedTextChange, formattingChanges[0]);
        }
    }
}
