// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Threading;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class FindAllReferencesHandlerTest
    {
        public FindAllReferencesHandlerTest()
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
            var projectionProvider = Mock.Of<LSPProjectionProvider>();
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>();
            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();
            var referencesHandler = new FindAllReferencesHandler(requestInvoker, documentManager, projectionProvider, documentMappingProvider, languageServiceBroker);
            var referenceRequest = new ReferenceParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await referencesHandler.HandleRequestAsync(referenceRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task HandleRequestAsync_ProjectionNotFound_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());
            var requestInvoker = Mock.Of<LSPRequestInvoker>();
            var projectionProvider = Mock.Of<LSPProjectionProvider>();
            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>();
            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();
            var referencesHandler = new FindAllReferencesHandler(requestInvoker, documentManager, projectionProvider, documentMappingProvider, languageServiceBroker);
            var referenceRequest = new ReferenceParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await referencesHandler.HandleRequestAsync(referenceRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task HandleRequestAsync_HtmlProjection_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());

            var requestInvoker = Mock.Of<LSPRequestInvoker>();
            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.Html,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>();
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>();
            var referencesHandler = new FindAllReferencesHandler(requestInvoker, documentManager, projectionProvider.Object, documentMappingProvider, languageServiceBroker);
            var referenceRequest = new ReferenceParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(0, 1)
            };

            // Act
            var result = await referencesHandler.HandleRequestAsync(referenceRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_InvokesCSharpLanguageServer()
        {
            // Arrange
            var lspFarEndpointCalled = false;
            var progressReported = false;
            var expectedUri1 = new Uri("C:/path/to/file1.razor");
            var expectedUri2 = new Uri("C:/path/to/file2.razor");
            var expectedLocation1 = GetReferenceItem(5, 5, 5, 5, expectedUri1);
            var expectedLocation2 = GetReferenceItem(10, 10, 10, 10, expectedUri2);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());

            var virtualCSharpUri1 = new Uri("C:/path/to/file1.razor.g.cs");
            var virtualCSharpUri2 = new Uri("C:/path/to/file2.razor.g.cs");
            var csharpLocation1 = GetReferenceItem(100, 100, 100, 100, virtualCSharpUri1);
            var csharpLocation2 = GetReferenceItem(200, 200, 200, 200, virtualCSharpUri2);
            var requestInvoker = new Mock<TestLSPRequestInvoker>();

            var parameterToken = new JObject
            {
                { "value", JArray.FromObject(new[] { csharpLocation1, csharpLocation2 }) }
            };

            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<ReferenceParams, VSReferenceItem[]>(It.IsAny<string>(), It.IsAny<LanguageServerKind>(), It.IsAny<ReferenceParams>(), It.IsAny<CancellationToken>()))
                .Callback<string, LanguageServerKind, ReferenceParams, CancellationToken>((method, serverKind, definitionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentReferencesName, method);
                    Assert.Equal(LanguageServerKind.CSharp, serverKind);
                    lspFarEndpointCalled = true;


                    requestInvoker.Object.RaiseNotification((Methods.ProgressNotificationName, parameterToken));
                })
                .Returns(Task.FromResult(Array.Empty<VSReferenceItem>()));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>();
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var remappingResult1 = new RazorMapToDocumentRangesResponse()
            {
                Ranges = new[] { expectedLocation1.Location.Range }
            };
            var remappingResult2 = new RazorMapToDocumentRangesResponse()
            {
                Ranges = new[] { expectedLocation2.Location.Range }
            };
            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>();
            documentMappingProvider
                .Setup(d => d.MapToDocumentRangesAsync(RazorLanguageKind.CSharp, It.IsAny<Uri>(), It.IsAny<Range[]>(), It.IsAny<CancellationToken>()))
                .Returns<RazorLanguageKind, Uri, Range[], CancellationToken>((languageKind, uri, ranges, ct) => Task.FromResult(uri.LocalPath.Contains("file1") ? remappingResult1 : remappingResult2));

            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();
            var referencesHandler = new FindAllReferencesHandler(requestInvoker.Object, documentManager, projectionProvider.Object, documentMappingProvider.Object, languageServiceBroker);
            requestInvoker.Object.AddClientNotifyAsyncHandler(referencesHandler.ClientNotifyAsyncListenerTest);

            var progressToken = new ProgressWithCompletion<object>((val) =>
            {
                var results = Assert.IsType<VSReferenceItem[]>(val);
                Assert.Collection(results,
                    a => AssertVSReferenceItem(expectedLocation1, a),
                    b => AssertVSReferenceItem(expectedLocation2, b));
                progressReported = true;
            });
            var referenceRequest = new ReferenceParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5),
                PartialResultToken = progressToken
            };

            // Act
            var result = await referencesHandler.HandleRequestAsync(referenceRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(lspFarEndpointCalled);
            Assert.True(progressReported);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_RemapsExternalRazorFiles()
        {
            // Arrange
            var progressReported = false;
            var externalUri = new Uri("C:/path/to/someotherfile.razor");
            var expectedLocation = GetReferenceItem(5, 5, 5, 5, externalUri);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 2));
            documentManager.AddDocument(externalUri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 5));

            var virtualCSharpUri = new Uri("C:/path/to/someotherfile.razor.g.cs");
            var csharpLocation = GetReferenceItem(100, 100, 100, 100, virtualCSharpUri);
            var requestInvoker = MockLSPRequestInvoker(csharpLocation);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>();
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var remappingResult = new RazorMapToDocumentRangesResponse()
            {
                Ranges = new[] { expectedLocation.Location.Range },
                HostDocumentVersion = 5
            };
            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>();
            documentMappingProvider.Setup(d => d.MapToDocumentRangesAsync(RazorLanguageKind.CSharp, externalUri, new[] { csharpLocation.Location.Range }, It.IsAny<CancellationToken>())).
                Returns(Task.FromResult(remappingResult));

            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

            var referencesHandler = new FindAllReferencesHandler(requestInvoker.Object, documentManager, projectionProvider.Object, documentMappingProvider.Object, languageServiceBroker);
            requestInvoker.Object.AddClientNotifyAsyncHandler(referencesHandler.ClientNotifyAsyncListenerTest);

            var progressToken = new ProgressWithCompletion<object>((val) =>
            {
                var results = Assert.IsType<VSReferenceItem[]>(val);
                var actualLocation = Assert.Single(results);
                AssertVSReferenceItem(expectedLocation, actualLocation);
                progressReported = true;
            });
            var referenceRequest = new ReferenceParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5),
                PartialResultToken = progressToken
            };

            // Act
            var result = await referencesHandler.HandleRequestAsync(referenceRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(progressReported);
        }

        [Fact]
        public async Task HandleRequestAsync_CSharpProjection_DoesNotRemapNonRazorFiles()
        {
            // Arrange
            var progressReported = false;
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());

            var externalCSharpUri = new Uri("C:/path/to/someotherfile.cs");
            var externalCsharpLocation = GetReferenceItem(100, 100, 100, 100, externalCSharpUri);
            var requestInvoker = MockLSPRequestInvoker(externalCsharpLocation);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>();
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>();
            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

            var referencesHandler = new FindAllReferencesHandler(requestInvoker.Object, documentManager, projectionProvider.Object, documentMappingProvider, languageServiceBroker);
            requestInvoker.Object.AddClientNotifyAsyncHandler(referencesHandler.ClientNotifyAsyncListenerTest);

            var progressToken = new ProgressWithCompletion<object>((val) =>
            {
                var results = Assert.IsType<VSReferenceItem[]>(val);
                var actualLocation = Assert.Single(results);
                AssertVSReferenceItem(externalCsharpLocation, actualLocation);
                progressReported = true;
            });
            var referenceRequest = new ReferenceParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5),
                PartialResultToken = progressToken
            };

            // Act
            var result = await referencesHandler.HandleRequestAsync(referenceRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(progressReported);
        }

        [Fact]
        public async Task HandleRequestAsync_VersionMismatch_DiscardsLocation()
        {
            // Arrange
            var progressReported = false;
            var expectedLocation = GetReferenceItem(5, 5, 5, 5, Uri);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 123));

            var virtualCSharpUri = new Uri("C:/path/to/file.razor.g.cs");
            var csharpLocation = GetReferenceItem(100, 100, 100, 100, virtualCSharpUri);
            var requestInvoker = MockLSPRequestInvoker(csharpLocation);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>();
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var remappingResult = new RazorMapToDocumentRangesResponse()
            {
                Ranges = new[] { expectedLocation.Location.Range },
                HostDocumentVersion = 122 // Different from document version (123)
            };
            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>();
            documentMappingProvider.Setup(d => d.MapToDocumentRangesAsync(RazorLanguageKind.CSharp, Uri, new[] { csharpLocation.Location.Range }, It.IsAny<CancellationToken>())).
                Returns(Task.FromResult(remappingResult));

            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

            var referencesHandler = new FindAllReferencesHandler(requestInvoker.Object, documentManager, projectionProvider.Object, documentMappingProvider.Object, languageServiceBroker);
            requestInvoker.Object.AddClientNotifyAsyncHandler(referencesHandler.ClientNotifyAsyncListenerTest);

            var progressToken = new ProgressWithCompletion<object>((val) =>
            {
                var results = Assert.IsType<VSReferenceItem[]>(val);
                Assert.Empty(results);
                progressReported = true;
            });
            var referenceRequest = new ReferenceParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5),
                PartialResultToken = progressToken
            };

            // Act
            var result = await referencesHandler.HandleRequestAsync(referenceRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(progressReported);
            Assert.Empty(result);
        }

        [Fact]
        public async Task HandleRequestAsync_VersionMismatch_DiscardsExternalRazorFiles()
        {
            // Arrange
            var progressReported = false;
            var externalUri = new Uri("C:/path/to/someotherfile.razor");
            var expectedLocation = GetReferenceItem(5, 5, 5, 5, externalUri);
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 2));
            documentManager.AddDocument(externalUri, Mock.Of<LSPDocumentSnapshot>(d => d.Version == 5));

            var virtualCSharpUri = new Uri("C:/path/to/someotherfile.razor.g.cs");
            var csharpLocation = GetReferenceItem(100, 100, 100, 100, virtualCSharpUri);
            var requestInvoker = MockLSPRequestInvoker(csharpLocation);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>();
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var remappingResult = new RazorMapToDocumentRangesResponse()
            {
                Ranges = new[] { expectedLocation.Location.Range },
                HostDocumentVersion = 6
            };
            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>();
            documentMappingProvider.Setup(d => d.MapToDocumentRangesAsync(RazorLanguageKind.CSharp, externalUri, new[] { csharpLocation.Location.Range }, It.IsAny<CancellationToken>())).
                Returns(Task.FromResult(remappingResult));

            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

            var referencesHandler = new FindAllReferencesHandler(requestInvoker.Object, documentManager, projectionProvider.Object, documentMappingProvider.Object, languageServiceBroker);
            requestInvoker.Object.AddClientNotifyAsyncHandler(referencesHandler.ClientNotifyAsyncListenerTest);

            var progressToken = new ProgressWithCompletion<object>((val) =>
            {
                var results = Assert.IsType<VSReferenceItem[]>(val);
                Assert.Empty(results);
                progressReported = true;
            });
            var referenceRequest = new ReferenceParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5),
                PartialResultToken = progressToken
            };

            // Act
            var result = await referencesHandler.HandleRequestAsync(referenceRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(progressReported);
            Assert.Empty(result);
        }

        [Fact]
        public async Task HandleRequestAsync_RemapFailure_DiscardsLocation()
        {
            // Arrange
            var progressReported = false;
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());

            var virtualCSharpUri = new Uri("C:/path/to/file.razor.g.cs");
            var csharpLocation = GetReferenceItem(100, 100, 100, 100, virtualCSharpUri);
            var requestInvoker = MockLSPRequestInvoker(csharpLocation);

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>();
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>();
            documentMappingProvider.Setup(d => d.MapToDocumentRangesAsync(RazorLanguageKind.CSharp, Uri, new[] { csharpLocation.Location.Range }, It.IsAny<CancellationToken>())).
                Returns(Task.FromResult<RazorMapToDocumentRangesResponse>(null));

            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

            var referencesHandler = new FindAllReferencesHandler(requestInvoker.Object, documentManager, projectionProvider.Object, documentMappingProvider.Object, languageServiceBroker);
            requestInvoker.Object.AddClientNotifyAsyncHandler(referencesHandler.ClientNotifyAsyncListenerTest);

            var progressToken = new ProgressWithCompletion<object>((val) =>
            {
                var results = Assert.IsType<VSReferenceItem[]>(val);
                Assert.Empty(results);
                progressReported = true;
            });
            var referenceRequest = new ReferenceParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5),
                PartialResultToken = progressToken
            };

            // Act
            var result = await referencesHandler.HandleRequestAsync(referenceRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(progressReported);
            Assert.Empty(result);
        }

        [Fact]
        public async Task HandleRequestAsync_LargeProject_InvokesCSharpLanguageServer()
        {
            // Validates batching mechanism for the progress notification on large projects

            // Arrange
            var lspFarEndpointCalled = false;
            var progressBatchesReported = 0;

            const int BATCH_SIZE = 10;
            const int NUM_BATCHES = 10;
            const int NUM_DOCUMENTS = BATCH_SIZE * NUM_BATCHES;
            const int MAPPING_OFFSET = 10;
            const int DELAY_BETWEEN_BATCHES_MS = 1250;

            var expectedUris = new Uri[NUM_DOCUMENTS];
            var virtualUris = new Uri[NUM_DOCUMENTS];
            var expectedReferences = new VSReferenceItem[NUM_BATCHES][];
            var csharpUnmappedReferences = new VSReferenceItem[NUM_BATCHES][];
            var parameterTokens = new JObject[NUM_BATCHES];

            var documentNumber = 0;
            for (var batch = 0; batch < NUM_BATCHES; ++batch)
            {
                expectedReferences[batch] = new VSReferenceItem[BATCH_SIZE];
                csharpUnmappedReferences[batch] = new VSReferenceItem[BATCH_SIZE];

                for (var documentInBatch = 0; documentInBatch < BATCH_SIZE; ++documentInBatch)
                {
                    expectedUris[documentNumber] = new Uri($"C:/path/to/file{documentNumber}.razor");
                    virtualUris[documentNumber] = new Uri($"C:/path/to/file{documentNumber}.razor.g.cs");
                    expectedReferences[batch][documentInBatch] = GetReferenceItem(documentNumber, expectedUris[documentNumber]);

                    var umappedOffset = documentNumber * MAPPING_OFFSET;
                    csharpUnmappedReferences[batch][documentInBatch] = GetReferenceItem(umappedOffset, virtualUris[documentNumber]);
                    documentNumber++;
                }

                parameterTokens[batch] = new JObject
                {
                    { "value", JArray.FromObject(csharpUnmappedReferences[batch]) }
                };
            }

            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(Uri, Mock.Of<LSPDocumentSnapshot>());

            var requestInvoker = new Mock<TestLSPRequestInvoker>();
            requestInvoker
                .Setup(r => r.ReinvokeRequestOnServerAsync<ReferenceParams, VSReferenceItem[]>(It.IsAny<string>(), It.IsAny<LanguageServerKind>(), It.IsAny<ReferenceParams>(), It.IsAny<CancellationToken>()))
                .Callback<string, LanguageServerKind, ReferenceParams, CancellationToken>(async (method, serverKind, definitionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentReferencesName, method);
                    Assert.Equal(LanguageServerKind.CSharp, serverKind);
                    lspFarEndpointCalled = true;

                    for (var i = 0; i < NUM_BATCHES; ++i)
                    {
                        await Task.Delay(DELAY_BETWEEN_BATCHES_MS);
                        requestInvoker.Object.RaiseNotification((Methods.ProgressNotificationName, parameterTokens[i]));
                    }
                })
                .Returns(Task.FromResult(Array.Empty<VSReferenceItem>()));

            var projectionResult = new ProjectionResult()
            {
                LanguageKind = RazorLanguageKind.CSharp,
            };
            var projectionProvider = new Mock<LSPProjectionProvider>();
            projectionProvider.Setup(p => p.GetProjectionAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(projectionResult));

            var documentMappingProvider = new Mock<LSPDocumentMappingProvider>();
            documentMappingProvider
                .Setup(d => d.MapToDocumentRangesAsync(RazorLanguageKind.CSharp, It.IsAny<Uri>(), It.IsAny<Range[]>(), It.IsAny<CancellationToken>()))
                .Returns<RazorLanguageKind, Uri, Range[], CancellationToken>((languageKind, uri, ranges, ct) => {
                    var unmappedPosition = ranges[0].Start.Line;
                    var mappedPosition = unmappedPosition / MAPPING_OFFSET;

                    var mappedRange = new Range()
                    {
                        Start = new Position(mappedPosition, mappedPosition),
                        End = new Position(mappedPosition, mappedPosition)
                    };

                    var response = new RazorMapToDocumentRangesResponse()
                    {
                        Ranges = new[] { mappedRange }
                    };

                    return Task.FromResult(response);
                });

            var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

            var referencesHandler = new FindAllReferencesHandler(requestInvoker.Object, documentManager, projectionProvider.Object, documentMappingProvider.Object, languageServiceBroker);
            requestInvoker.Object.AddClientNotifyAsyncHandler(referencesHandler.ClientNotifyAsyncListenerTest);

            var progressToken = new ProgressWithCompletion<object>((val) =>
            {
                var results = Assert.IsType<VSReferenceItem[]>(val);
                Assert.Equal(BATCH_SIZE, results.Length);

                for (var i = 0; i < BATCH_SIZE; ++i)
                {
                    AssertVSReferenceItem(expectedReferences[progressBatchesReported][i], results[i]);
                }

                ++progressBatchesReported;
            });
            var referenceRequest = new ReferenceParams()
            {
                TextDocument = new TextDocumentIdentifier() { Uri = Uri },
                Position = new Position(10, 5),
                PartialResultToken = progressToken
            };

            // Act
            var result = await referencesHandler.HandleRequestAsync(referenceRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.True(lspFarEndpointCalled);
            Assert.Equal(NUM_BATCHES, progressBatchesReported);
        }

        //[Fact]
        //public async Task HandleRequestAsync_EnsureClientNotifyAsyncLifecycle()
        //{
        //    // Arrange
        //    var documentManager = new TestDocumentManager();
        //    var requestInvoker = new TestLSPRequestInvokerImplementation();
        //    var projectionProvider = Mock.Of<LSPProjectionProvider>();
        //    var documentMappingProvider = Mock.Of<LSPDocumentMappingProvider>();
        //    var languageServiceBroker = Mock.Of<ILanguageServiceBroker2>();

        //    var referenceRequest = new ReferenceParams()
        //    {
        //        TextDocument = new TextDocumentIdentifier() { Uri = Uri },
        //        Position = new Position(10, 5)
        //    };

        //    // Act
        //    using (var referencesHandler = new FindAllReferencesHandler(requestInvoker, documentManager, projectionProvider, documentMappingProvider, languageServiceBroker))
        //    {
        //        var result = await referencesHandler.HandleRequestAsync(referenceRequest, new ClientCapabilities(), CancellationToken.None).ConfigureAwait(false);

        //        // Assert
        //        Assert.Empty(result);
        //        Assert.True(requestInvoker.clientNotifyAdded);
        //        Assert.False(requestInvoker.clientNotifyRemoved);
        //    }

        //    Assert.True(requestInvoker.clientNotifyRemoved);
        //}

        private bool AssertVSReferenceItem(VSReferenceItem expected, VSReferenceItem actual)
        {
            Assert.Equal(expected.Location, actual.Location);
            Assert.Equal(expected.DisplayPath, actual.DisplayPath);
            return true;
        }

        private Mock<TestLSPRequestInvoker> MockLSPRequestInvoker(VSReferenceItem csharpLocation)
        {
            var requestInvoker = new Mock<TestLSPRequestInvoker>();

            var parameterToken = new JObject
            {
                { "value", JArray.FromObject(new[] { csharpLocation }) }
            };

            requestInvoker.Setup(i => i.ReinvokeRequestOnServerAsync<ReferenceParams, VSReferenceItem[]>(
                It.IsAny<string>(), It.IsAny<LanguageServerKind>(), It.IsAny<ReferenceParams>(), It.IsAny<CancellationToken>()))
                .Callback<string, LanguageServerKind, ReferenceParams, CancellationToken>((method, serverKind, definitionParams, ct) =>
                {
                    Assert.Equal(Methods.TextDocumentReferencesName, method);
                    Assert.Equal(LanguageServerKind.CSharp, serverKind);

                    requestInvoker.Object.RaiseNotification((Methods.ProgressNotificationName, parameterToken));
                })
                .Returns(Task.FromResult(Array.Empty<VSReferenceItem>()));

            return requestInvoker;
        }

        private VSReferenceItem GetReferenceItem(int position, Uri uri)
        {
            return GetReferenceItem(position, position, position, position, uri);
        }

        private VSReferenceItem GetReferenceItem(
            int startLine,
            int startCharacter,
            int endLine,
            int endCharacter,
            Uri uri,
            string documentName = "document",
            string projectName = "project",
            string text = "text")
        {
            return new VSReferenceItem()
            {
                Location = new Location()
                {
                    Uri = uri,
                    Range = new Range()
                    {
                        Start = new Position(startLine, startCharacter),
                        End = new Position(endLine, endCharacter)
                    }
                },
                DocumentName = documentName,
                ProjectName = projectName,
                DisplayPath = uri.AbsolutePath,
                Text = text
            };
        }
    }

    internal abstract class TestLSPRequestInvoker : LSPRequestInvoker
    {
        public event AsyncEventHandler<(string MethodName, JToken ParameterToken)> ClientNotifyAsync;

        internal void AddClientNotifyAsyncHandler(AsyncEventHandler<(string MethodName, JToken ParameterToken)> clientNotifyAsyncHandler)
        {
            ClientNotifyAsync += clientNotifyAsyncHandler;
        }

        internal void RemoveClientNotifyAsyncHandler(AsyncEventHandler<(string MethodName, JToken ParameterToken)> clientNotifyAsyncHandler)
        {
            ClientNotifyAsync -= clientNotifyAsyncHandler;
        }

        public void RaiseNotification((string MethodName, JToken ParameterToken) args)
        {
            ClientNotifyAsync(sender: null, args);
        }
    }
}
