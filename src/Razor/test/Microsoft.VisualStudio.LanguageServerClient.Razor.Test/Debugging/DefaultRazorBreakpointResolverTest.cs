// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Editor.Razor.Debugging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Test;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Debugging
{
    public class DefaultRazorBreakpointResolverTest
    {
        private const string ValidBreakpointCSharp = "private int foo = 123;";
        private const string InvalidBreakpointCSharp = "private int bar;";

        public DefaultRazorBreakpointResolverTest()
        {
            DocumentUri = new Uri("file://C:/path/to/file.razor", UriKind.Absolute);
            CSharpDocumentUri = new Uri(DocumentUri.OriginalString + ".g.cs", UriKind.Absolute);

            var csharpTextSnapshot = new StringTextSnapshot(
$@"public class SomeRazorFile
{{
    {ValidBreakpointCSharp}
    {InvalidBreakpointCSharp}
}}");
            CSharpTextBuffer = new TestTextBuffer(csharpTextSnapshot);

            var textBufferSnapshot = new StringTextSnapshot(@$"
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
        public async Task TryResolveBreakpointRangeAsync_NotValidBreakpointLocation_ReturnsNull()
        {
            // Arrange
            var hostDocumentPosition = GetPosition(InvalidBreakpointCSharp, HostTextbuffer);
            var resolver = CreateResolverWith();

            // Act
            var breakpointRange = await resolver.TryResolveBreakpointRangeAsync(HostTextbuffer, hostDocumentPosition.Line, hostDocumentPosition.Character, CancellationToken.None);

            // Assert
            Assert.Null(breakpointRange);
        }

        [Fact]
        public async Task TryResolveBreakpointRangeAsync_MappableCSharpBreakpointLocation_ReturnsHostBreakpointLocation()
        {
            // Arrange
            var hostDocumentPosition = GetPosition(ValidBreakpointCSharp, HostTextbuffer);
            var hostBreakpointRange = new Range()
            {
                Start = hostDocumentPosition,
                End = new Position(hostDocumentPosition.Line, hostDocumentPosition.Character + ValidBreakpointCSharp.Length),
            };
            var projectionProvider = new TestLSPBreakpointSpanProvider(
                DocumentUri,
                new Dictionary<Position, Range>()
                {
                    [hostDocumentPosition] = hostBreakpointRange
                });
            var resolver = CreateResolverWith(projectionProvider: projectionProvider);

            // Act
            var breakpointRange = await resolver.TryResolveBreakpointRangeAsync(HostTextbuffer, hostDocumentPosition.Line, hostDocumentPosition.Character, CancellationToken.None);

            // Assert
            Assert.Equal(hostBreakpointRange, breakpointRange);
        }

        private RazorBreakpointResolver CreateResolverWith(
            FileUriProvider uriProvider = null,
            LSPDocumentManager documentManager = null,
            LSPBreakpointSpanProvider projectionProvider = null)
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
                    .Returns(Task.FromResult<Range>(null));
            }

            var razorBreakpointResolver = new DefaultRazorBreakpointResolver(uriProvider, documentManager, projectionProvider);

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
    }
}
