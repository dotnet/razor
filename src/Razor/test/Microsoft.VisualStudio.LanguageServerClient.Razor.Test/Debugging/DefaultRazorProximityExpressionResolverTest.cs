// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Editor.Razor.Debugging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Debugging
{
    public class DefaultRazorProximityExpressionResolverTest
    {
        public DefaultRazorProximityExpressionResolverTest()
        {
            DocumentUri = new Uri("file://C:/path/to/file.razor", UriKind.Absolute);
            CSharpDocumentUri = new Uri(DocumentUri.OriginalString + ".g.cs", UriKind.Absolute);

            ValidProximityExpressionRoot = "var abc = 123;";
            InvalidProximityExpressionRoot = "private int bar;";
            var csharpTextSnapshot = new StringTextSnapshot(
$@"public class SomeRazorFile
{{
    {InvalidProximityExpressionRoot}

    public void Render()
    {{
        {ValidProximityExpressionRoot}
    }}
}}
");
            CSharpTextBuffer = new TestTextBuffer(csharpTextSnapshot);

            var textBufferSnapshot = new StringTextSnapshot($"@{{{InvalidProximityExpressionRoot}}} @code {{{ValidProximityExpressionRoot}}}");
            HostTextbuffer = new TestTextBuffer(textBufferSnapshot);
        }

        private string ValidProximityExpressionRoot { get; }

        private string InvalidProximityExpressionRoot { get; }

        private ITextBuffer CSharpTextBuffer { get; }

        private Uri DocumentUri { get; }

        private Uri CSharpDocumentUri { get; }

        private ITextBuffer HostTextbuffer { get; }

        [Fact]
        public async Task TryResolveProximityExpressionsAsync_UnaddressableTextBuffer_ReturnsNull()
        {
            // Arrange
            var differentTextBuffer = Mock.Of<ITextBuffer>(MockBehavior.Strict);
            var resolver = CreateResolverWith();

            // Act
            var expressions = await resolver.TryResolveProximityExpressionsAsync(differentTextBuffer, lineIndex: 0, characterIndex: 1, CancellationToken.None);

            // Assert
            Assert.Null(expressions);
        }

        [Fact]
        public async Task TryResolveProximityExpressionsAsync_UnknownRazorDocument_ReturnsNull()
        {
            // Arrange
            var documentManager = new Mock<LSPDocumentManager>(MockBehavior.Strict).Object;
            Mock.Get(documentManager).Setup(m => m.TryGetDocument(DocumentUri, out It.Ref<LSPDocumentSnapshot>.IsAny)).Returns(false);
            var resolver = CreateResolverWith(documentManager: documentManager);

            // Act
            var result = await resolver.TryResolveProximityExpressionsAsync(HostTextbuffer, lineIndex: 0, characterIndex: 1, CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task TryResolveProximityExpressionsAsync_UnsynchronizedCSharpDocument_ReturnsNull()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var testCSharpDocument = new CSharpVirtualDocumentSnapshot(CSharpDocumentUri, CSharpTextBuffer.CurrentSnapshot, hostDocumentSyncVersion: 1);
            var document = new TestLSPDocumentSnapshot(DocumentUri, version: (int)(testCSharpDocument.HostDocumentSyncVersion.Value + 1), testCSharpDocument);
            documentManager.AddDocument(document.Uri, document);
            var resolver = CreateResolverWith(documentManager: documentManager);

            // Act
            var expressions = await resolver.TryResolveProximityExpressionsAsync(HostTextbuffer, lineIndex: 0, characterIndex: 1, CancellationToken.None);

            // Assert
            Assert.Null(expressions);
        }

        private RazorProximityExpressionResolver CreateResolverWith(
            FileUriProvider uriProvider = null,
            LSPDocumentManager documentManager = null)
        {
            var documentUri = DocumentUri;
            uriProvider ??= Mock.Of<FileUriProvider>(provider => provider.TryGet(HostTextbuffer, out documentUri) == true && provider.TryGet(It.IsNotIn(HostTextbuffer), out It.Ref<Uri>.IsAny) == false, MockBehavior.Strict);
            var csharpVirtualDocumentSnapshot = new CSharpVirtualDocumentSnapshot(CSharpDocumentUri, CSharpTextBuffer.CurrentSnapshot, hostDocumentSyncVersion: 0);
            LSPDocumentSnapshot documentSnapshot = new TestLSPDocumentSnapshot(DocumentUri, 0, csharpVirtualDocumentSnapshot);
            documentManager ??= Mock.Of<LSPDocumentManager>(manager => manager.TryGetDocument(DocumentUri, out documentSnapshot) == true, MockBehavior.Strict);

            var razorProximityExpressionResolver = new DefaultRazorProximityExpressionResolver(
                uriProvider,
                documentManager,
                TestLSPProximityExpressionProvider.Instance);

            return razorProximityExpressionResolver;
        }

        private class TestWorkspaceAccessor : VisualStudioWorkspaceAccessor
        {
            public static readonly TestWorkspaceAccessor Instance = new TestWorkspaceAccessor();

            private TestWorkspaceAccessor()
            {
            }

            public override bool TryGetWorkspace(ITextBuffer textBuffer, out CodeAnalysis.Workspace workspace)
            {
                workspace = TestWorkspace.Create();
                return true;
            }
        }

        private class TestLSPProximityExpressionProvider : LSPProximityExpressionsProvider
        {
            public static readonly TestLSPProximityExpressionProvider Instance = new();

            private TestLSPProximityExpressionProvider()
            {
            }

            public override Task<IReadOnlyList<string>> GetProximityExpressionsAsync(LSPDocumentSnapshot documentSnapshot, Position position, CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<string>>(null);
            }
        }
    }
}
