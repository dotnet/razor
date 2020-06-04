// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class SignatureHelpHandlerTest
    {
        public SignatureHelpHandlerTest()
        {
            Uri = new Uri("C:/path/to/file.razor");

            SignatureHelp = new SignatureHelp()
            {
                ActiveSignature = 0,
                ActiveParameter = 1,
                Signatures = new[]
                {
                    new SignatureInformation()
                    {
                        Documentation = "This does something",
                        Label = "Let's do something",
                        Parameters = new[]
                        {
                            new ParameterInformation()
                            {
                                Documentation = "First param part one",
                                Label = "First param label"
                            },
                            new ParameterInformation()
                            {
                                Documentation = "Second param part two",
                                Label = "Second param label"
                            }
                        }
                    },

                    new SignatureInformation()
                    {
                        Documentation = "This does something else",
                        Label = "Let's do something else",
                        Parameters = new[]
                        {
                            new ParameterInformation()
                            {
                                Documentation = "First param part one",
                                Label = "First param label"
                            },
                            new ParameterInformation()
                            {
                                Documentation = "Second param part two",
                                Label = "Second param label"
                            }
                        }
                    }
                }
            };
        }

        private Uri Uri { get; }

        private SignatureHelp SignatureHelp { get; }

        [Fact]
        public async Task HandleRequestAsync_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var requestInvoker = Mock.Of<LSPRequestInvoker>();
            var projectionProvider = Mock.Of<LSPProjectionProvider>();
            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker, documentManager, projectionProvider);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await signatureHelpHandler.HandleRequestAsync(signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_ProjectionNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());
            var requestInvoker = Mock.Of<LSPRequestInvoker>();
            var projectionProvider = Mock.Of<LSPProjectionProvider>();
            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker, documentManager, projectionProvider);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await signatureHelpHandler.HandleRequestAsync(signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_HtmlProjection_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());
            var requestInvoker = Mock.Of<LSPRequestInvoker>();

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>();
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker, documentManager, projectionProvider.Object);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await signatureHelpHandler.HandleRequestAsync(signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_InvokesCSharpLanguageServer_ReturnsItem()
        {
            // Arrange
            var called = false;
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());

            var virtualCSharpUri = new Uri("C:/path/to/file.razor.g.cs");
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, SignatureHelp>(It.IsAny<string>(), It.IsAny<LanguageServerKind>(), It.IsAny<TextDocumentPositionParams>(), It.IsAny<CancellationToken>()))
                .Callback<string, LanguageServerKind, TextDocumentPositionParams, CancellationToken>((method, serverKind, definitionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentSignatureHelpName, method);
                    Assert.Equal(LanguageServerKind.CSharp, serverKind);
                    called = true;
                })
                .Returns(Task.FromResult(SignatureHelp));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker.Object, documentManager, projectionProvider.Object);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await signatureHelpHandler.HandleRequestAsync(signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            Assert.Equal(SignatureHelp, result);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_InvokesCSharpLanguageServer_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 123));

            var virtualCSharpUri = new Uri("C:/path/to/file.razor.g.cs");
            var requestInvoker = Mock.Of<LSPRequestInvoker>(i =>
                    i.ReinvokeRequestOnServerAsync<TextDocumentPositionParams, SignatureHelp>(It.IsAny<string>(), It.IsAny<LanguageServerKind>(), It.IsAny<TextDocumentPositionParams>(), It.IsAny<CancellationToken>()) == Task.FromResult<SignatureHelp>(null));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>(MockBehavior.Strict);
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var signatureHelpHandler = new SignatureHelpHandler(requestInvoker, documentManager, projectionProvider.Object);
            var signatureHelpRequest = new TextDocumentPositionParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5)
            };

            // Act
            var result = await signatureHelpHandler.HandleRequestAsync(signatureHelpRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }
    }
}
