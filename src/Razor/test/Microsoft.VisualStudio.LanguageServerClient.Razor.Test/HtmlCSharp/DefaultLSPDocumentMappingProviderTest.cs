// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public class DefaultLSPDocumentMappingProviderTest
    {
        public Uri RazorFile => new Uri("file:///some/folder/to/file.razor");

        public Uri RazorVirtualCSharpFile => new Uri("file:///some/folder/to/file.razor.g.cs");

        public Uri AnotherRazorFile => new Uri("file:///some/folder/to/anotherfile.razor");

        public Uri AnotherRazorVirtualCSharpFile => new Uri("file:///some/folder/to/anotherfile.razor.g.cs");

        public Uri CSharpFile => new Uri("file:///some/folder/to/csharpfile.cs");

        [Fact]
        public async Task RazorMapToDocumentRangeAsync_InvokesLanguageServer()
        {
            // Arrange
            var uri = new Uri("file:///some/folder/to/file.razor");

            var response = new RazorMapToDocumentRangeResponse()
            {
                Range = new Range()
                {
                    Start = new Position(1, 1),
                    End = new Position(3, 3),
                },
                HostDocumentVersion = 1
            };
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
            requestInvoker
                .Setup(r => r.CustomRequestServerAsync<RazorMapToDocumentRangeParams, RazorMapToDocumentRangeResponse>(LanguageServerConstants.RazorMapToDocumentRangeEndpoint, LanguageServerKind.Razor, It.IsAny<RazorMapToDocumentRangeParams>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(response));

            var documentManager = new TestDocumentManager();
            var mappingProvider = new DefaultLSPDocumentMappingProvider(requestInvoker.Object, documentManager);
            var projectedRange = new Range()
            {
                Start = new Position(10, 10),
                End = new Position(15, 15)
            };

            // Act
            var result = await mappingProvider.MapToDocumentRangeAsync(RazorLanguageKind.CSharp, uri, projectedRange, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.HostDocumentVersion);
            Assert.Equal(new Position(1, 1), result.Range.Start);
            Assert.Equal(new Position(3, 3), result.Range.End);
        }

        [Fact]
        public async Task RemapWorkspaceEditAsync_RemapsEditsAsExpected()
        {
            // Arrange
            var expectedRange = new TestRange(1, 1, 1, 5);
            var expectedVersion = 1;
            var documentManager = new TestDocumentManager();
            documentManager.AddDocument(RazorFile, Mock.Of<LSPDocumentSnapshot>(d => d.Version == expectedVersion && d.Uri == RazorFile));

            var requestInvoker = GetRequestInvoker(new[]
            {
                ((RazorLanguageKind.CSharp, RazorFile, new TestRange(10, 10, 10, 15)), (expectedRange, expectedVersion))
            });
            var mappingProvider = new DefaultLSPDocumentMappingProvider(requestInvoker, documentManager);

            var workspaceEdit = new TestWorkspaceEdit(versionedEdits: true);
            workspaceEdit.AddEdits(RazorVirtualCSharpFile, 10, new TestTextEdit("newText", new TestRange(10, 10, 10, 15)));

            // Act
            var result = await mappingProvider.RemapWorkspaceEditAsync(workspaceEdit, CancellationToken.None).ConfigureAwait(false);

            // Assert
            var documentEdit = Assert.Single(result.DocumentChanges);
            Assert.Equal(RazorFile, documentEdit.TextDocument.Uri);
            Assert.Equal(expectedVersion, documentEdit.TextDocument.Version);

            var actualEdit = Assert.Single(documentEdit.Edits);
            Assert.Equal("newText", actualEdit.NewText);
            Assert.Equal(expectedRange, actualEdit.Range);
        }

        private LSPRequestInvoker GetRequestInvoker(((RazorLanguageKind, Uri, TestRange), (TestRange, int))[] mappingPairs)
        {
            var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);

            // mappingPairs will contain the request/response pair for each of MapToDocumentRange LSP request we want to mock.
            foreach (var ((kind, uri, projectedRange), (mappedRange, version)) in mappingPairs)
            {
                var requestParams = new RazorMapToDocumentRangeParams()
                {
                    Kind = kind,
                    RazorDocumentUri = uri,
                    ProjectedRange = projectedRange
                };
                var response = new RazorMapToDocumentRangeResponse()
                {
                    Range = mappedRange,
                    HostDocumentVersion = version
                };

                requestInvoker
                    .Setup(r => r.CustomRequestServerAsync<RazorMapToDocumentRangeParams, RazorMapToDocumentRangeResponse>(LanguageServerConstants.RazorMapToDocumentRangeEndpoint, LanguageServerKind.Razor, requestParams, It.IsAny<CancellationToken>()))
                    .Returns(Task.FromResult(response));
            }

            return requestInvoker.Object;
        }

        private class TestWorkspaceEdit : WorkspaceEdit
        {
            public TestWorkspaceEdit(bool versionedEdits = false)
            {
                if (versionedEdits)
                {
                    DocumentChanges = Array.Empty<TextDocumentEdit>();
                }

                Changes = new Dictionary<string, TextEdit[]>();
            }

            public void AddEdits(Uri uri, int version, params TextEdit[] edits)
            {
                Changes[uri.GetAbsoluteOrUNCPath()] = edits;

                DocumentChanges = DocumentChanges?.Append(new TextDocumentEdit()
                {
                    Edits = edits,
                    TextDocument = new VersionedTextDocumentIdentifier()
                    {
                        Uri = uri,
                        Version = version
                    }
                }).ToArray();
            }
        }

        private class TestTextEdit : TextEdit
        {
            public TestTextEdit(string newText, Range range)
            {
                NewText = newText;
                Range = range;
            }
        }

        private class TestRange : Range
        {
            public TestRange(int startLine, int startCharacter, int endLine, int endCharacter)
            {
                Start = new Position(startLine, startCharacter);
                End = new Position(endLine, endCharacter);
            }
        }
    }
}
