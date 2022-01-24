// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Moq;
using Xunit;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Debugging
{
    public class DefaultRazorBreakpointResolverTest
    {
        private const string ValidBreakpointInlineCSharp = "var abc = 123;";
        private const string InvalidBreakpointInlineCSharp = "var goo = 456;";
        private const string ValidBreakpointCSharp = "private int foo = 123;";
        private const string InvalidBreakpointCSharp = "private int bar;";

        public DefaultRazorBreakpointResolverTest()
        {
            CSHTMLDocumentUri = new Uri("file://C:/path/to/file.cshtml", UriKind.Absolute);
            DocumentUri = new Uri("file://C:/path/to/file.razor", UriKind.Absolute);
            CSharpDocumentUri = new Uri(DocumentUri.OriginalString + ".g.cs", UriKind.Absolute);

            var csharpTextSnapshot = new StringTextSnapshot(
$@"public class SomeRazorFile
{{
    void Render()
    {{
        {ValidBreakpointInlineCSharp}
        {InvalidBreakpointInlineCSharp}
    }}
    
    {ValidBreakpointCSharp}
    {InvalidBreakpointCSharp}
}}");
            CSharpTextBuffer = new TestTextBuffer(csharpTextSnapshot);

            var textBufferSnapshot = new StringTextSnapshot(@$"
<p>@{{ {ValidBreakpointInlineCSharp} }}</p>
<p>@{{
    {InvalidBreakpointInlineCSharp}
}}</p>

@code
{{
    {ValidBreakpointCSharp}
    {InvalidBreakpointCSharp}
}}
");
            HostTextbuffer = new TestTextBuffer(textBufferSnapshot);
        }

        private ITextBuffer CSharpTextBuffer { get; }

        private Uri DocumentUri { get; }

        private Uri CSHTMLDocumentUri { get; }

        private Uri CSharpDocumentUri { get; }

        private ITextBuffer HostTextbuffer { get; }

        [Fact]
        public async Task TryResolveBreakpointRangeAsync_UnaddressableTextBuffer_ReturnsNull()
        {
            // Arrange
            var differentTextBuffer = Mock.Of<ITextBuffer>(MockBehavior.Strict);
            var resolver = CreateResolverWith();

            // Act
            var breakpointRange = await resolver.TryResolveBreakpointRangeAsync(differentTextBuffer, lineIndex: 0, characterIndex: 1, CancellationToken.None);

            // Assert
            Assert.Null(breakpointRange);
        }

        [Fact]
        public async Task TryResolveBreakpointRangeAsync_UnknownRazorDocument_ReturnsNull()
        {
            // Arrange
            var documentManager = new Mock<LSPDocumentManager>(MockBehavior.Strict).Object;
            Mock.Get(documentManager).Setup(m => m.TryGetDocument(DocumentUri, out It.Ref<LSPDocumentSnapshot>.IsAny)).Returns(false);
            var resolver = CreateResolverWith(documentManager: documentManager);

            // Act
            var result = await resolver.TryResolveBreakpointRangeAsync(HostTextbuffer, lineIndex: 0, characterIndex: 1, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task TryResolveBreakpointRangeAsync_UnsynchronizedCSharpDocument_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var testCSharpDocument = new CSharpVirtualDocumentSnapshot(CSharpDocumentUri, CSharpTextBuffer.CurrentSnapshot, hostDocumentSyncVersion: 1);
            var document = new TestLSPDocumentSnapshot(DocumentUri, version: (int)(testCSharpDocument.HostDocumentSyncVersion.Value + 1), testCSharpDocument);
            documentManager.AddDocument(document.Uri, document);
            var resolver = CreateResolverWith(documentManager: documentManager);

            // Act
            var expressions = await resolver.TryResolveBreakpointRangeAsync(HostTextbuffer, lineIndex: 0, characterIndex: 1, CancellationToken.None);

            // Assert
            Assert.Null(expressions);
        }

        [Fact]
        public async Task TryResolveBreakpointRangeAsync_UnprojectedLocation_ReturnsNull()
        {
            // Arrange
            var resolver = CreateResolverWith();

            // Act
            var breakpointRange = await resolver.TryResolveBreakpointRangeAsync(HostTextbuffer, lineIndex: 0, characterIndex: 1, CancellationToken.None);

            // Assert
            Assert.Null(breakpointRange);
        }

        [Fact]
        public async Task TryResolveBreakpointRangeAsync_RazorProjectedLocation_ReturnsNull()
        {
            // Arrange
            var position = new Position(line: 0, character: 2);
            var projectionProvider = new TestLSPBreakpointSpanProvider(
                DocumentUri,
                new Dictionary<Position, ProjectionResult>()
                {
                    [position] = new ProjectionResult()
                    {
                        LanguageKind = RazorLanguageKind.Razor,
                        HostDocumentVersion = 0,
                        Position = position,
                    }
                });
            var resolver = CreateResolverWith(projectionProvider: projectionProvider);

            // Act
            var breakpointRange = await resolver.TryResolveBreakpointRangeAsync(HostTextbuffer, position.Line, position.Character, CancellationToken.None);

            // Assert
            Assert.Null(breakpointRange);
        }

        [Fact]
        public async Task TryResolveBreakpointRangeAsync_NotValidBreakpointLocation_ReturnsNull()
        {
            // Arrange
            var hostDocumentPosition = GetPosition(InvalidBreakpointCSharp, HostTextbuffer);
            var csharpDocumentPosition = GetPosition(InvalidBreakpointCSharp, CSharpTextBuffer);
            var csharpDocumentIndex = CSharpTextBuffer.CurrentSnapshot.GetText().IndexOf(InvalidBreakpointCSharp, StringComparison.Ordinal);
            var projectionProvider = new TestLSPBreakpointSpanProvider(
                DocumentUri,
                new Dictionary<Position, ProjectionResult>()
                {
                    [hostDocumentPosition] = new ProjectionResult()
                    {
                        LanguageKind = RazorLanguageKind.CSharp,
                        HostDocumentVersion = 0,
                        Position = csharpDocumentPosition,
                        PositionIndex = csharpDocumentIndex,
                    }
                });
            var resolver = CreateResolverWith(projectionProvider: projectionProvider);

            // Act
            var breakpointRange = await resolver.TryResolveBreakpointRangeAsync(HostTextbuffer, hostDocumentPosition.Line, hostDocumentPosition.Character, CancellationToken.None);

            // Assert
            Assert.Null(breakpointRange);
        }

        [Fact]
        public async Task TryResolveBreakpointRangeAsync_UnmappableCSharpBreakpointLocation_ReturnsNull()
        {
            // Arrange
            var hostDocumentPosition = GetPosition(ValidBreakpointCSharp, HostTextbuffer);
            var csharpDocumentPosition = GetPosition(ValidBreakpointCSharp, CSharpTextBuffer);
            var csharpDocumentIndex = CSharpTextBuffer.CurrentSnapshot.GetText().IndexOf(ValidBreakpointCSharp, StringComparison.Ordinal);
            var projectionProvider = new TestLSPBreakpointSpanProvider(
                DocumentUri,
                new Dictionary<Position, ProjectionResult>()
                {
                    [hostDocumentPosition] = new ProjectionResult()
                    {
                        LanguageKind = RazorLanguageKind.CSharp,
                        HostDocumentVersion = 0,
                        Position = csharpDocumentPosition,
                        PositionIndex = csharpDocumentIndex,
                    }
                });
            var resolver = CreateResolverWith(projectionProvider: projectionProvider);

            // Act
            var breakpointRange = await resolver.TryResolveBreakpointRangeAsync(HostTextbuffer, hostDocumentPosition.Line, hostDocumentPosition.Character, CancellationToken.None);

            // Assert
            Assert.Null(breakpointRange);
        }

        [Fact]
        public void GetMappingBehavior_CSHTML()
        {
            // Act
            var result = DefaultRazorBreakpointResolver.GetMappingBehavior(CSHTMLDocumentUri);

            // Assert
            Assert.Equal(LanguageServerMappingBehavior.Inclusive, result);
        }

        [Fact]
        public void GetMappingBehavior_Razor()
        {
            // Act
            var result = DefaultRazorBreakpointResolver.GetMappingBehavior(DocumentUri);

            // Assert
            Assert.Equal(LanguageServerMappingBehavior.Strict, result);
        }

        [Fact]
        public async Task TryResolveBreakpointRangeAsync_MappableCSharpBreakpointLocation_ReturnsHostBreakpointLocation()
        {
            // Arrange
            var hostDocumentPosition = GetPosition(ValidBreakpointCSharp, HostTextbuffer);
            var csharpDocumentPosition = GetPosition(ValidBreakpointCSharp, CSharpTextBuffer);
            var csharpDocumentIndex = CSharpTextBuffer.CurrentSnapshot.GetText().IndexOf(ValidBreakpointCSharp, StringComparison.Ordinal);
            var projectionProvider = new TestLSPBreakpointSpanProvider(
                DocumentUri,
                new Dictionary<Position, ProjectionResult>()
                {
                    [hostDocumentPosition] = new ProjectionResult()
                    {
                        LanguageKind = RazorLanguageKind.CSharp,
                        HostDocumentVersion = 0,
                        Position = csharpDocumentPosition,
                        PositionIndex = csharpDocumentIndex,
                    }
                });
            var expectedCSharpBreakpointRange = new Range()
            {
                Start = csharpDocumentPosition,
                End = new Position(csharpDocumentPosition.Line, csharpDocumentPosition.Character + ValidBreakpointCSharp.Length),
            };
            var hostBreakpointRange = new Range()
            {
                Start = hostDocumentPosition,
                End = new Position(hostDocumentPosition.Line, hostDocumentPosition.Character + ValidBreakpointCSharp.Length),
            };
            var mappingProvider = new TestLSPDocumentMappingProvider(
                new Dictionary<Range, RazorMapToDocumentRangesResponse>()
                {
                    [expectedCSharpBreakpointRange] = new RazorMapToDocumentRangesResponse()
                    {
                        HostDocumentVersion = 0,
                        Ranges = new[]
                        {
                            hostBreakpointRange,
                        },
                    }
                });
            var resolver = CreateResolverWith(projectionProvider: projectionProvider, documentMappingProvider: mappingProvider);

            // Act
            var breakpointRange = await resolver.TryResolveBreakpointRangeAsync(HostTextbuffer, hostDocumentPosition.Line, hostDocumentPosition.Character, CancellationToken.None);

            // Assert
            Assert.Equal(hostBreakpointRange, breakpointRange);
        }

        [Fact]
        public async Task TryResolveBreakpointRangeAsync_InlineCSharp_ReturnsFirstCSharpCodeOnLine()
        {
            // Arrange
            var hostDocumentPosition = GetPosition(ValidBreakpointInlineCSharp, HostTextbuffer);
            var csharpDocumentPosition = GetPosition(ValidBreakpointInlineCSharp, CSharpTextBuffer);
            var csharpDocumentIndex = CSharpTextBuffer.CurrentSnapshot.GetText().IndexOf(ValidBreakpointInlineCSharp, StringComparison.Ordinal);
            var projectionProvider = new TestLSPBreakpointSpanProvider(
                DocumentUri,
                new Dictionary<Position, ProjectionResult>()
                {
                    [hostDocumentPosition] = new ProjectionResult()
                    {
                        LanguageKind = RazorLanguageKind.CSharp,
                        HostDocumentVersion = 0,
                        Position = csharpDocumentPosition,
                        PositionIndex = csharpDocumentIndex,
                    }
                });
            var expectedCSharpBreakpointRange = new Range()
            {
                Start = csharpDocumentPosition,
                End = new Position(csharpDocumentPosition.Line, csharpDocumentPosition.Character + ValidBreakpointInlineCSharp.Length),
            };
            var hostBreakpointRange = new Range()
            {
                Start = hostDocumentPosition,
                End = new Position(hostDocumentPosition.Line, hostDocumentPosition.Character + ValidBreakpointInlineCSharp.Length),
            };
            var mappingProvider = new TestLSPDocumentMappingProvider(
                new Dictionary<Range, RazorMapToDocumentRangesResponse>()
                {
                    [expectedCSharpBreakpointRange] = new RazorMapToDocumentRangesResponse()
                    {
                        HostDocumentVersion = 0,
                        Ranges = new[]
                        {
                            hostBreakpointRange,
                        },
                    }
                });
            var resolver = CreateResolverWith(projectionProvider: projectionProvider, documentMappingProvider: mappingProvider);

            // Act
            var breakpointRange = await resolver.TryResolveBreakpointRangeAsync(HostTextbuffer, hostDocumentPosition.Line, characterIndex: 0, CancellationToken.None);

            // Assert
            Assert.Equal(hostBreakpointRange, breakpointRange);
        }

        [Fact]
        public async Task TryResolveBreakpointRangeAsync_InvalidInlineCSharp_ReturnsNull()
        {
            // Arrange
            var hostDocumentPosition = GetPosition(InvalidBreakpointInlineCSharp, HostTextbuffer);
            var csharpDocumentPosition = GetPosition(InvalidBreakpointInlineCSharp, CSharpTextBuffer);
            var csharpDocumentIndex = CSharpTextBuffer.CurrentSnapshot.GetText().IndexOf(InvalidBreakpointInlineCSharp, StringComparison.Ordinal);
            var projectionProvider = new TestLSPBreakpointSpanProvider(
                DocumentUri,
                new Dictionary<Position, ProjectionResult>()
                {
                    [hostDocumentPosition] = new ProjectionResult()
                    {
                        LanguageKind = RazorLanguageKind.CSharp,
                        HostDocumentVersion = 0,
                        Position = csharpDocumentPosition,
                        PositionIndex = csharpDocumentIndex,
                    }
                });
            var expectedCSharpBreakpointRange = new Range()
            {
                Start = csharpDocumentPosition,
                End = new Position(csharpDocumentPosition.Line, csharpDocumentPosition.Character + InvalidBreakpointInlineCSharp.Length),
            };
            var hostBreakpointRange = new Range()
            {
                Start = hostDocumentPosition,
                End = new Position(hostDocumentPosition.Line, hostDocumentPosition.Character + InvalidBreakpointInlineCSharp.Length),
            };
            var mappingProvider = new TestLSPDocumentMappingProvider(
                new Dictionary<Range, RazorMapToDocumentRangesResponse>()
                {
                    [expectedCSharpBreakpointRange] = new RazorMapToDocumentRangesResponse()
                    {
                        HostDocumentVersion = 0,
                        Ranges = new[]
                        {
                            hostBreakpointRange,
                        },
                    }
                });
            var resolver = CreateResolverWith(projectionProvider: projectionProvider, documentMappingProvider: mappingProvider);

            // Act
            var breakpointRange = await resolver.TryResolveBreakpointRangeAsync(HostTextbuffer, hostDocumentPosition.Line - 1, characterIndex: 0, CancellationToken.None);

            // Assert
            Assert.Null(breakpointRange);
        }

        private RazorBreakpointResolver CreateResolverWith(
            FileUriProvider uriProvider = null,
            LSPDocumentManager documentManager = null,
            LSPBreakpointSpanProvider projectionProvider = null,
            LSPDocumentMappingProvider documentMappingProvider = null)
        {
            var documentUri = DocumentUri;
            uriProvider ??= Mock.Of<FileUriProvider>(provider => provider.TryGet(HostTextbuffer, out documentUri) == true && provider.TryGet(It.IsNotIn(HostTextbuffer), out It.Ref<Uri>.IsAny) == false, MockBehavior.Strict);
            var csharpVirtualDocumentSnapshot = new CSharpVirtualDocumentSnapshot(CSharpDocumentUri, CSharpTextBuffer.CurrentSnapshot, hostDocumentSyncVersion: 0);
            LSPDocumentSnapshot documentSnapshot = new TestLSPDocumentSnapshot(DocumentUri, 0, csharpVirtualDocumentSnapshot);
            documentManager ??= Mock.Of<LSPDocumentManager>(manager => manager.TryGetDocument(DocumentUri, out documentSnapshot) == true, MockBehavior.Strict);
            if (projectionProvider is null)
            {
                projectionProvider = new Mock<LSPBreakpointSpanProvider>(MockBehavior.Strict).Object;
                Mock.Get(projectionProvider).Setup(projectionProvider => projectionProvider.GetBreakpointSpanAsync(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<Position>(), CancellationToken.None))
                    .Returns(Task.FromResult<ProjectionResult>(null));
            }

            if (documentMappingProvider is null)
            {
                documentMappingProvider = new Mock<LSPDocumentMappingProvider>(MockBehavior.Strict).Object;
                Mock.Get(documentMappingProvider).Setup(p => p.MapToDocumentRangesAsync(It.IsAny<RazorLanguageKind>(), It.IsAny<Uri>(), It.IsAny<Range[]>(), LanguageServerMappingBehavior.Strict, CancellationToken.None))
                    .Returns(Task.FromResult<RazorMapToDocumentRangesResponse>(null));
            }

            var razorBreakpointResolver = DefaultRazorBreakpointResolver.CreateTestInstance(
                uriProvider,
                documentManager,
                projectionProvider,
                documentMappingProvider);

            return razorBreakpointResolver;
        }

        private static Position GetPosition(string content, ITextBuffer textBuffer)
        {
            var index = textBuffer.CurrentSnapshot.GetText().IndexOf(content, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(content));
            }

            textBuffer.CurrentSnapshot.GetLineAndCharacter(index, out var lineIndex, out var characterIndex);
            return new Position(lineIndex, characterIndex);
        }

        private class TestLSPDocumentMappingProvider : LSPDocumentMappingProvider
        {
            private readonly IReadOnlyDictionary<Range, RazorMapToDocumentRangesResponse> _mappings;

            public TestLSPDocumentMappingProvider(IReadOnlyDictionary<Range, RazorMapToDocumentRangesResponse> mappings)
            {
                if (mappings is null)
                {
                    throw new ArgumentNullException(nameof(mappings));
                }

                _mappings = mappings;
            }

            public override Task<RazorMapToDocumentRangesResponse> MapToDocumentRangesAsync(RazorLanguageKind languageKind, Uri razorDocumentUri, Range[] projectedRanges, CancellationToken cancellationToken)
                => MapToDocumentRangesAsync(languageKind, razorDocumentUri, projectedRanges, LanguageServerMappingBehavior.Strict, cancellationToken);

            public override Task<RazorMapToDocumentRangesResponse> MapToDocumentRangesAsync(
                RazorLanguageKind languageKind,
                Uri razorDocumentUri,
                Range[] projectedRanges,
                LanguageServerMappingBehavior mappingBehavior,
                CancellationToken cancellationToken)
            {
                _mappings.TryGetValue(projectedRanges[0], out var response);
                return Task.FromResult(response);
            }

            public override Task<TextEdit[]> RemapFormattedTextEditsAsync(Uri uri, TextEdit[] edits, FormattingOptions options, bool containsSnippet, CancellationToken cancellationToken) => throw new NotImplementedException();

            public override Task<Location[]> RemapLocationsAsync(Location[] locations, CancellationToken cancellationToken) => throw new NotImplementedException();

            public override Task<TextEdit[]> RemapTextEditsAsync(Uri uri, TextEdit[] edits, CancellationToken cancellationToken) => throw new NotImplementedException();

            public override Task<WorkspaceEdit> RemapWorkspaceEditAsync(WorkspaceEdit workspaceEdit, CancellationToken cancellationToken) => throw new NotImplementedException();
        }
    }
}
