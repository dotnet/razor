// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class DocumentPullDiagnosticsHandlerTest
    {
        private static readonly Diagnostic ValidDiagnostic_UnknownName = new Diagnostic()
        {
            Range = new Range()
            {
                Start = new Position(149, 19),
                End = new Position(149, 23)
            },
            Code = "CS0103",
            Source = "DocumentPullDiagnosticHandler",
            Message = "The name 'saflkjklj' does not exist in the current context"
        };

        private static readonly Diagnostic ValidDiagnostic_InvalidExpression = new Diagnostic()
        {
            Range = new Range()
            {
                Start = new Position(150, 19),
                End = new Position(150, 23)
            },
            Code = "CS1525",
            Source = "DocumentPullDiagnosticHandler",
            Message = "Invalid expression term 'bool'"
        };

        private static readonly Diagnostic UnusedUsingsDiagnostic = new Diagnostic()
        {
            Range = new Range()
            {
                Start = new Position(151, 19),
                End = new Position(151, 23)
            },
            Code = "IDE0005_gen",
            Source = "DocumentPullDiagnosticHandler",
            Message = "Using directive is unnecessary."
        };

        private static readonly Diagnostic RemoveUnnecessaryImportsFixableDiagnostic = new Diagnostic()
        {
            Range = new Range()
            {
                Start = new Position(152, 19),
                End = new Position(152, 23)
            },
            Code = "RemoveUnnecessaryImportsFixable",
            Source = "DocumentPullDiagnosticHandler",
        };

        private static readonly DiagnosticReport[] RoslynDiagnosticResponse = new DiagnosticReport[]
        {
            new DiagnosticReport()
            {
                ResultId = "5",
                Diagnostics = new Diagnostic[]
                {
                    ValidDiagnostic_UnknownName,
                    ValidDiagnostic_InvalidExpression,
                    UnusedUsingsDiagnostic,
                    RemoveUnnecessaryImportsFixableDiagnostic
                }
            }
        };

        public DocumentPullDiagnosticsHandlerTest()
        {
            Uri = new Uri("C:/path/to/file.razor");
        }

        private Uri Uri { get; }

        [Fact]
        public async Task HandleRequestAsync_DocumentNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var requestInvoker = Mock.Of<LSPRequestInvoker>();
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>();
            var documentDiagnosticsHandler = new DocumentPullDiagnosticsHandler(requestInvoker, documentManager, documentMappingProvider);
            var diagnosticRequest = new DocumentDiagnosticsParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                PreviousResultId = "4"
            };

            // Act
            var result = await documentDiagnosticsHandler.HandleRequestAsync(diagnosticRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

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
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>();
            var documentDiagnosticsHandler = new DocumentPullDiagnosticsHandler(requestInvoker, documentManager, documentMappingProvider);
            var diagnosticRequest = new DocumentDiagnosticsParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                PreviousResultId = "4"
            };

            // Act
            var result = await documentDiagnosticsHandler.HandleRequestAsync(diagnosticRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(result);
        }

        // [Fact]
        // public async Task HandleRequestAsync_CSharpProjection_RemapsDiagnosticRange()
        // {
        //     // Arrange
        //     var called = false;
        //     var documentManager = new TestDocumentManager();
        //     documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 0));

        //     var requestInvoker = GetRequestInvoker<DocumentDiagnosticsParams, DiagnosticReport[]>(
        //         new[] { RoslynDiagnosticResponse },
        //         (method, serverContentType, diagnosticParams, ct) =>
        //         {
        //             Assert.Equal(MSLSPMethods.DocumentPullDiagnosticName, method);
        //             Assert.Equal(RazorLSPConstants.CSharpContentTypeName, serverContentType);
        //             called = true;
        //         });

        //     var documentMappingProvider = GetDocumentMappingProvider(expectedHighlight.Range, 0, RazorLanguageKind.CSharp);

        //     var documentDiagnosticsHandler = new DocumentPullDiagnosticsHandler(requestInvoker, documentManager, documentMappingProvider);
        //     var diagnosticRequest = new DocumentDiagnosticsParams()
        //     {
        //         TextDocument = new TextDocumentIdentifier() { Uri = Uri },
        //         PreviousResultId = "4"
        //     };

        //     // Act
        //     var result = await documentDiagnosticsHandler.HandleRequestAsync(diagnosticRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

        //     // Assert
        //     Assert.True(called);
        //     var actualDiagnostics = Assert.Single(result);
        //     Assert.Equal(expectedHighlight.Range, actualDiagnostics.Range);
        // }

        // [Fact]
        // public async Task HandleRequestAsync_HtmlProjection_RemapsHighlightRange()
        // {
        //     // Arrange
        //     var called = false;
        //     var expectedHighlight = GetHighlight(5, 5, 5, 5);
        //     var documentManager = new TestDocumentManager();
        //     documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 0));

        //     var htmlHighlight = GetHighlight(100, 100, 100, 100);
        //     var requestInvoker = GetRequestInvoker<DocumentDiagnosticsParams, DiagnosticReport[]>(
        //         new[] { htmlHighlight },
        //         (method, serverContentType, diagnosticParams, ct) =>
        //         {
        //             Assert.Equal(MSLSPMethods.DocumentPullDiagnosticName, method);
        //             Assert.Equal(RazorLSPConstants.HtmlLSPContentTypeName, serverContentType);
        //             called = true;
        //         });

        //     var documentMappingProvider = GetDocumentMappingProvider(expectedHighlight.Range, 0, RazorLanguageKind.Html);

        //     var documentDiagnosticsHandler = new DocumentPullDiagnosticsHandler(requestInvoker, documentManager, documentMappingProvider);
        //     var diagnosticRequest = new DocumentDiagnosticsParams()
        //     {
        //         TextDocument = new TextDocumentIdentifier() { Uri = Uri },
        //         PreviousResultId = "4"
        //     };

        //     // Act
        //     var result = await documentDiagnosticsHandler.HandleRequestAsync(diagnosticRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

        //     // Assert
        //     Assert.True(called);
        //     var actualDiagnostics = Assert.Single(result);
        //     Assert.Equal(expectedHighlight.Range, actualDiagnostics.Range);
        // }

        // [Fact]
        // public async Task HandleRequestAsync_VersionMismatch_DiscardsLocation()
        // {
        //     // Arrange
        //     var called = false;
        //     var expectedHighlight = GetHighlight(5, 5, 5, 5);
        //     var documentManager = new TestDocumentManager();
        //     documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 1));

        //     var csharpHighlight = GetHighlight(100, 100, 100, 100);
        //     var requestInvoker = GetRequestInvoker<DocumentDiagnosticsParams, DiagnosticReport[]>(
        //         new[] { csharpHighlight },
        //         (method, serverContentType, diagnosticParams, ct) =>
        //         {
        //             Assert.Equal(MSLSPMethods.DocumentPullDiagnosticName, method);
        //             Assert.Equal(RazorLSPConstants.CSharpContentTypeName, serverContentType);
        //             called = true;
        //         });

        //     var documentMappingProvider = Mock.Of<TestLSPDocumentMappingProvider>();
        //     documentMappingProvider.HostDocumentVersion = 0; // Different from document version (1)

        //     var documentDiagnosticsHandler = new DocumentPullDiagnosticsHandler(requestInvoker, documentManager, documentMappingProvider);
        //     var diagnosticRequest = new DocumentDiagnosticsParams()
        //     {
        //         TextDocument = new TextDocumentIdentifier() { Uri = Uri },
        //         PreviousResultId = "4"
        //     };

        //     // Act
        //     var result = await documentDiagnosticsHandler.HandleRequestAsync(diagnosticRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

        //     // Assert
        //     Assert.True(called);
        //     Assert.Empty(result);
        // }

        // [Fact]
        // public async Task HandleRequestAsync_RemapFailure_DiscardsLocation()
        // {
        //     // Arrange
        //     var called = false;
        //     var expectedHighlight = GetHighlight(5, 5, 5, 5);
        //     var documentManager = new TestDocumentManager();
        //     documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 0));

        //     var csharpHighlight = GetHighlight(100, 100, 100, 100);
        //     var requestInvoker = GetRequestInvoker<DocumentDiagnosticsParams, DiagnosticReport[]>(
        //         new[] { csharpHighlight },
        //         (method, serverContentType, diagnosticParams, ct) =>
        //         {
        //             Assert.Equal(MSLSPMethods.DocumentPullDiagnosticName, method);
        //             Assert.Equal(RazorLSPConstants.CSharpContentTypeName, serverContentType);
        //             called = true;
        //         });

        //     var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>();

        //     var documentDiagnosticsHandler = new DocumentPullDiagnosticsHandler(requestInvoker, documentManager, documentMappingProvider);
        //     var diagnosticRequest = new DocumentDiagnosticsParams()
        //     {
        //         TextDocument = new TextDocumentIdentifier() { Uri = Uri },
        //         PreviousResultId = "4"
        //     };

        //     // Act
        //     var result = await documentDiagnosticsHandler.HandleRequestAsync(diagnosticRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

        //     // Assert
        //     Assert.True(called);
        //     Assert.Empty(result);
        // }

        private LSPRequestInvoker GetRequestInvoker<TParams, TResult>(TResult expectedResponse, Action<string, string, TParams, CancellationToken> callback)
        {
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<TParams, TResult>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TParams>(), It.IsAny<CancellationToken>()))
                .Callback(callback)
                .Returns(Task.FromResult(expectedResponse));

            return requestInvoker.Object;
        }

        private abstract class TestLSPDocumentMappingProvider : LSPDocumentMappingProvider
        {
            public long HostDocumentVersion { get; set; }

            public override Task<RazorMapToDocumentRangesResponse> MapToDocumentRangesAsync(
                RazorLanguageKind languageKind,
                Uri razorDocumentUri,
                Range[] projectedRanges,
                LanguageServerMappingBehavior mappingBehavior,
                CancellationToken cancellationToken)
            {
                Assert.Equal(LanguageServerMappingBehavior.Inclusive, mappingBehavior);
                Assert.Equal(RazorLanguageKind.CSharp, languageKind);

                var res = new RazorMapToDocumentRangesResponse()
                {
                    Ranges = projectedRanges.Select(r =>
                        new Range()
                        {
                            Start = new Position(r.Start.Line - 100, r.Start.Character),
                            End = new Position(r.End.Line - 100, r.End.Character)
                        }
                    ).ToArray(),
                    HostDocumentVersion = HostDocumentVersion
                };

                return Task.FromResult(res);
            }
        }
    }
}
