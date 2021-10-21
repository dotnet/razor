// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Moq;
using Xunit;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class DocumentPullDiagnosticsHandlerTest
    {
        private static readonly Diagnostic s_validDiagnostic_UnknownName = new()
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

        private static readonly Range s_validDiagnostic_UnknownName_MappedRange = new()
        {
            Start = new Position(49, 19),
            End = new Position(49, 23)
        };

        private static readonly Diagnostic s_validDiagnostic_InvalidExpression = new()
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

        private static readonly Range s_validDiagnostic_InvalidExpression_MappedRange = new()
        {
            Start = new Position(50, 19),
            End = new Position(50, 23)
        };

        private static readonly Diagnostic s_unusedUsingsDiagnostic = new()
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

        private static readonly Diagnostic s_removeUnnecessaryImportsFixableDiagnostic = new()
        {
            Range = new Range()
            {
                Start = new Position(152, 19),
                End = new Position(152, 23)
            },
            Code = "RemoveUnnecessaryImportsFixable",
            Source = "DocumentPullDiagnosticHandler",
        };

        private static readonly VSInternalDiagnosticReport[] s_roslynDiagnosticResponse = new VSInternalDiagnosticReport[]
        {
            new VSInternalDiagnosticReport()
            {
                ResultId = "5",
                Diagnostics = new Diagnostic[]
                {
                    s_validDiagnostic_UnknownName,
                    s_validDiagnostic_InvalidExpression,
                    s_unusedUsingsDiagnostic,
                    s_removeUnnecessaryImportsFixableDiagnostic
                }
            }
        };

        public DocumentPullDiagnosticsHandlerTest()
        {
            Uri = new Uri("C:/path/to/file.razor");

            var logger = new Mock<ILogger>(MockBehavior.Strict).Object;
            Mock.Get(logger).Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>())).Verifiable();
            LoggerProvider = Mock.Of<HTMLCSharpLanguageServerLogHubLoggerProvider>(l => l.CreateLogger(It.IsAny<string>()) == logger, MockBehavior.Strict);
        }

        private Uri Uri { get; }
        private HTMLCSharpLanguageServerLogHubLoggerProvider LoggerProvider { get; }

        [Fact]
        public async Task HandleRequestAsync_DocumentNotFound_ClearsDiagnostics()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var requestInvoker = Mock.Of<LSPRequestInvoker>(MockBehavior.Strict);
            var diagnosticsProvider = Mock.Of<LSPDiagnosticsTranslator>(MockBehavior.Strict);
            var documentSynchronizer = Mock.Of<LSPDocumentSynchronizer>(MockBehavior.Strict);
            var documentDiagnosticsHandler = new DocumentPullDiagnosticsHandler(requestInvoker, documentManager, documentSynchronizer, diagnosticsProvider, LoggerProvider);
            var diagnosticRequest = new VSInternalDocumentDiagnosticsParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                PreviousResultId = "4"
            };

            // Act
            var result = await documentDiagnosticsHandler.HandleRequestAsync(diagnosticRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            var report = Assert.Single(result);
            Assert.Null(report.Diagnostics);
            Assert.Null(report.ResultId);
        }

        [Fact]
        public async Task HandleRequestAsync_RemapsDiagnosticRange()
        {
            // Arrange
            var called = false;
            var documentManager = CreateDocumentManager();

            var requestInvoker = GetRequestInvoker<VSInternalDocumentDiagnosticsParams, VSInternalDiagnosticReport[]>(
                s_roslynDiagnosticResponse,
                (textBuffer, method, diagnosticParams, ct) =>
                {
                    Assert.Equal(VSInternalMethods.DocumentPullDiagnosticName, method);
                    called = true;
                });

            var diagnosticsProvider = GetDiagnosticsProvider(s_validDiagnostic_UnknownName_MappedRange, s_validDiagnostic_InvalidExpression_MappedRange);
            var documentSynchronizer = CreateDocumentSynchronizer();

            var documentDiagnosticsHandler = new DocumentPullDiagnosticsHandler(requestInvoker, documentManager, documentSynchronizer, diagnosticsProvider, LoggerProvider);
            var diagnosticRequest = new VSInternalDocumentDiagnosticsParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                PreviousResultId = "4"
            };

            // Act
            var result = await documentDiagnosticsHandler.HandleRequestAsync(diagnosticRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            var diagnosticReport = Assert.Single(result);
            Assert.Equal(s_roslynDiagnosticResponse.First().ResultId, diagnosticReport.ResultId);
            Assert.Collection(diagnosticReport.Diagnostics,
                d =>
                {
                    Assert.Equal(s_validDiagnostic_UnknownName.Code, d.Code);
                    Assert.Equal(s_validDiagnostic_UnknownName_MappedRange, d.Range);
                },
                d =>
                {
                    Assert.Equal(s_validDiagnostic_InvalidExpression.Code, d.Code);
                    Assert.Equal(s_validDiagnostic_InvalidExpression_MappedRange, d.Range);
                });
        }

        [Fact]
        public async Task HandleRequestAsync_DocumentSynchronizationFails_ReturnsNullDiagnostic()
        {
            // Arrange
            var called = false;
            var documentManager = CreateDocumentManager();

            var requestInvoker = GetRequestInvoker<VSInternalDocumentDiagnosticsParams, VSInternalDiagnosticReport[]>(
                s_roslynDiagnosticResponse,
                (textBuffer, method, diagnosticParams, ct) =>
                {
                    Assert.Equal(VSInternalMethods.DocumentPullDiagnosticName, method);
                    called = true;
                });

            var diagnosticsProvider = GetDiagnosticsProvider(s_validDiagnostic_UnknownName_MappedRange, s_validDiagnostic_InvalidExpression_MappedRange);

            var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);
            documentSynchronizer
                .Setup(d => d.TrySynchronizeVirtualDocumentAsync(It.IsAny<int>(), It.IsAny<CSharpVirtualDocumentSnapshot>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            var documentDiagnosticsHandler = new DocumentPullDiagnosticsHandler(requestInvoker, documentManager, documentSynchronizer.Object, diagnosticsProvider, LoggerProvider);
            var diagnosticRequest = new VSInternalDocumentDiagnosticsParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                PreviousResultId = "4"
            };

            // Act
            var result = await documentDiagnosticsHandler.HandleRequestAsync(diagnosticRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.False(called);
            var diagnosticReport = Assert.Single(result);
            Assert.Equal(diagnosticRequest.PreviousResultId, diagnosticReport.ResultId);
            Assert.Null(diagnosticReport.Diagnostics);
        }

        [Fact]
        public async Task HandleRequestAsync_RemapFailsButErrorDiagnosticIsShown()
        {
            // Arrange
            var called = false;
            var documentManager = CreateDocumentManager();

            var unmappableDiagnostic_errorSeverity = new Diagnostic()
            {
                Range = new Range()
                {
                    Start = new Position(149, 19),
                    End = new Position(149, 23)
                },
                Code = "CS0103",
                Severity = DiagnosticSeverity.Error
            };

            var unmappableDiagnostic_warningSeverity = new Diagnostic()
            {
                Range = new Range()
                {
                    Start = new Position(159, 19),
                    End = new Position(159, 23)
                },
                Code = "IDE003",
                Severity = DiagnosticSeverity.Warning
            };

            var diagnosticReport = new VSInternalDiagnosticReport()
            {
                ResultId = "6",
                Diagnostics = new Diagnostic[]
                {
                    unmappableDiagnostic_errorSeverity,
                    unmappableDiagnostic_warningSeverity
                }
            };

            var requestInvoker = GetRequestInvoker<VSInternalDocumentDiagnosticsParams, VSInternalDiagnosticReport[]>(
                new[] { diagnosticReport },
                (textBuffer, method, diagnosticParams, ct) =>
                {
                    Assert.Equal(VSInternalMethods.DocumentPullDiagnosticName, method);
                    called = true;
                });

            var undefinedRange = new Range() { Start = new Position(-1, -1), End = new Position(-1, -1) };
            var diagnosticsProvider = GetDiagnosticsProvider(undefinedRange, undefinedRange);

            var documentSynchronizer = CreateDocumentSynchronizer();

            var documentDiagnosticsHandler = new DocumentPullDiagnosticsHandler(requestInvoker, documentManager, documentSynchronizer, diagnosticsProvider, LoggerProvider);
            var diagnosticRequest = new VSInternalDocumentDiagnosticsParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                PreviousResultId = "4"
            };

            // Act
            var result = await documentDiagnosticsHandler.HandleRequestAsync(diagnosticRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);

            var diagnosticReportResult = Assert.Single(result);
            Assert.Equal(diagnosticReport.ResultId, diagnosticReportResult.ResultId);

            var returnedDiagnostic = Assert.Single(diagnosticReportResult.Diagnostics);
            Assert.Equal(unmappableDiagnostic_errorSeverity.Code, returnedDiagnostic.Code);
            Assert.True(returnedDiagnostic.Range.IsUndefined());
        }

        [Fact]
        public async Task HandleRequestAsync_NoDiagnosticsAfterFiltering_ReturnsNullDiagnostic()
        {
            // Arrange
            var called = false;
            var documentManager = CreateDocumentManager();

            var filteredDiagnostic = new Diagnostic()
            {
                Range = new Range()
                {
                    Start = new Position(159, 19),
                    End = new Position(159, 23)
                },
                Code = "RemoveUnnecessaryImportsFixable",
                Severity = DiagnosticSeverity.Warning
            };

            var filteredDiagnostic_mappedRange = new Range()
            {
                Start = new Position(49, 19),
                End = new Position(49, 23)
            };

            var diagnosticReport = new VSInternalDiagnosticReport()
            {
                ResultId = "6",
                Diagnostics = new Diagnostic[]
                {
                    filteredDiagnostic
                }
            };

            var requestInvoker = GetRequestInvoker<VSInternalDocumentDiagnosticsParams, VSInternalDiagnosticReport[]>(
                new[] { diagnosticReport },
                (textBuffer, method, diagnosticParams, ct) =>
                {
                    Assert.Equal(VSInternalMethods.DocumentPullDiagnosticName, method);
                    called = true;
                });

            var diagnosticsProvider = GetDiagnosticsProvider(filteredDiagnostic_mappedRange);
            var documentSynchronizer = CreateDocumentSynchronizer();

            var documentDiagnosticsHandler = new DocumentPullDiagnosticsHandler(requestInvoker, documentManager, documentSynchronizer, diagnosticsProvider, LoggerProvider);
            var diagnosticRequest = new VSInternalDocumentDiagnosticsParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                PreviousResultId = "4"
            };

            // Act
            var result = await documentDiagnosticsHandler.HandleRequestAsync(diagnosticRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            var returnedReport = Assert.Single(result);
            Assert.Equal(diagnosticReport.ResultId, returnedReport.ResultId);
            Assert.Empty(returnedReport.Diagnostics);
        }

        [Fact]
        public async Task HandleRequestAsync_VersionMismatch_DiscardsLocation()
        {
            // Arrange
            var called = false;
            var documentManager = CreateDocumentManager(hostDocumentVersion: 1);

            var requestInvoker = GetRequestInvoker<VSInternalDocumentDiagnosticsParams, VSInternalDiagnosticReport[]>(
                s_roslynDiagnosticResponse,
                (textBuffer, method, diagnosticParams, ct) =>
                {
                    Assert.Equal(VSInternalMethods.DocumentPullDiagnosticName, method);
                    called = true;
                });

            // Note the HostDocumentVersion provided by the DiagnosticsProvider = 0,
            // which is different from document version (1) from the DocumentManager
            var diagnosticsProvider = GetDiagnosticsProvider(s_validDiagnostic_UnknownName_MappedRange, s_validDiagnostic_InvalidExpression_MappedRange);
            var documentSynchronizer = CreateDocumentSynchronizer();

            var documentDiagnosticsHandler = new DocumentPullDiagnosticsHandler(requestInvoker, documentManager, documentSynchronizer, diagnosticsProvider, LoggerProvider);
            var diagnosticRequest = new VSInternalDocumentDiagnosticsParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                PreviousResultId = "4"
            };

            // Act
            var result = await documentDiagnosticsHandler.HandleRequestAsync(diagnosticRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            var returnedReport = Assert.Single(result);
            Assert.Equal(s_roslynDiagnosticResponse.First().ResultId, returnedReport.ResultId);
            Assert.Null(returnedReport.Diagnostics);
        }

        [Fact]
        public async Task HandleRequestAsync_RemapFailure_DiscardsLocation()
        {
            // Arrange
            var called = false;
            var documentManager = CreateDocumentManager();

            var requestInvoker = GetRequestInvoker<VSInternalDocumentDiagnosticsParams, VSInternalDiagnosticReport[]>(
                s_roslynDiagnosticResponse,
                (textBuffer, method, diagnosticParams, ct) =>
                {
                    Assert.Equal(VSInternalMethods.DocumentPullDiagnosticName, method);
                    called = true;
                });

            var diagnosticsProvider = GetDiagnosticsProvider();
            var documentSynchronizer = CreateDocumentSynchronizer();

            var documentDiagnosticsHandler = new DocumentPullDiagnosticsHandler(requestInvoker, documentManager, documentSynchronizer, diagnosticsProvider, LoggerProvider);
            var diagnosticRequest = new VSInternalDocumentDiagnosticsParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                PreviousResultId = "4"
            };

            // Act
            var result = await documentDiagnosticsHandler.HandleRequestAsync(diagnosticRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(called);
            var returnedReport = Assert.Single(result);
            Assert.Equal(s_roslynDiagnosticResponse.First().ResultId, returnedReport.ResultId);
            Assert.Null(returnedReport.Diagnostics);
        }

        private static LSPRequestInvoker GetRequestInvoker<TParams, TResult>(TResult expectedResponse, Action<ITextBuffer, string, TParams, CancellationToken> callback)
        {
            async IAsyncEnumerable<ReinvocationResponse<TResult>> GetExpectedResultsAsync()
            {
                yield return new ReinvocationResponse<TResult>("LanguageClientName", expectedResponse);

                await Task.CompletedTask;
            }

            var expectedResponses = GetExpectedResultsAsync();
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnMultipleServersAsync<TParams, TResult>(
                    It.IsAny<ITextBuffer>(), It.IsAny<string>(), It.IsAny<TParams>(), It.IsAny<CancellationToken>()))
                .Callback(callback)
                .Returns(expectedResponses);

            return requestInvoker.Object;
        }

        private LSPDocumentManager CreateDocumentManager(int hostDocumentVersion = 0)
        {
            var testVirtualDocUri = new Uri("C:/path/to/file.razor.g.cs");
            var testVirtualDocument = new TestVirtualDocumentSnapshot(Uri, hostDocumentVersion);
            var csharpTextBuffer = new TestTextBuffer(new StringTextSnapshot(string.Empty));
            var csharpVirtualDocument = new CSharpVirtualDocumentSnapshot(testVirtualDocUri, csharpTextBuffer.CurrentSnapshot, hostDocumentVersion);
            LSPDocumentSnapshot documentSnapshot = new TestLSPDocumentSnapshot(Uri, hostDocumentVersion, testVirtualDocument, csharpVirtualDocument);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, documentSnapshot);
            return documentManager;
        }

        private LSPDiagnosticsTranslator GetDiagnosticsProvider(params Range[] expectedRanges)
        {
            var diagnosticsToIgnore = new HashSet<string>()
            {
                "RemoveUnnecessaryImportsFixable",
                "IDE0005_gen", // Using directive is unnecessary
            };

            var diagnosticsProvider = new Mock<LSPDiagnosticsTranslator>(MockBehavior.Strict);
            diagnosticsProvider.Setup(d =>
                d.TranslateAsync(
                    RazorLanguageKind.CSharp,
                    Uri,
                    It.IsAny<Diagnostic[]>(),
                    It.IsAny<CancellationToken>()))
                .Returns((RazorLanguageKind lang, Uri uri, Diagnostic[] diagnostics, CancellationToken ct) =>
                {
                    // Indicates we're mocking mapping failed
                    if (expectedRanges.Length == 0)
                    {
                        return Task.FromResult(new RazorDiagnosticsResponse()
                        {
                            Diagnostics = null,
                            HostDocumentVersion = 0
                        });
                    }

                    var filteredDiagnostics = diagnostics.Where(d => !CanDiagnosticBeFiltered(d)).ToArray();
                    if (filteredDiagnostics.Length == 0)
                    {
                        return Task.FromResult(new RazorDiagnosticsResponse()
                        {
                            Diagnostics = Array.Empty<Diagnostic>(),
                            HostDocumentVersion = 0
                        });
                    }

                    var mappedDiagnostics = new List<Diagnostic>();

                    for (var i = 0; i < filteredDiagnostics.Length; i++)
                    {
                        var diagnostic = filteredDiagnostics[i];
                        var range = expectedRanges[i];

                        if (range.IsUndefined())
                        {
                            if (diagnostic.Severity != DiagnosticSeverity.Error)
                            {
                                continue;
                            }
                        }

                        diagnostic.Range = range;
                        mappedDiagnostics.Add(diagnostic);
                    }

                    return Task.FromResult(new RazorDiagnosticsResponse()
                    {
                        Diagnostics = mappedDiagnostics.ToArray(),
                        HostDocumentVersion = 0
                    });

                    bool CanDiagnosticBeFiltered(Diagnostic d)
                    {
                        return diagnosticsToIgnore.Contains(d.Code.Value.Second) &&
d.Severity != DiagnosticSeverity.Error;
                    }
                });

            return diagnosticsProvider.Object;
        }

        private static LSPDocumentSynchronizer CreateDocumentSynchronizer()
        {
            var documentSynchronizer = new Mock<LSPDocumentSynchronizer>(MockBehavior.Strict);
            documentSynchronizer
                .Setup(d => d.TrySynchronizeVirtualDocumentAsync(It.IsAny<int>(), It.IsAny<CSharpVirtualDocumentSnapshot>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            return documentSynchronizer.Object;
        }
    }
}
