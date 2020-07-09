using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage.Test
{
    public class VirtualDocumentFactoryBaseTest
    {
        public VirtualDocumentFactoryBaseTest()
        {
            ContentTypeRegistry = Mock.Of<IContentTypeRegistryService>();
            var textBufferFactory = new Mock<ITextBufferFactoryService>();
            textBufferFactory
                .Setup(factory => factory.CreateTextBuffer())
                .Returns(Mock.Of<ITextBuffer>(buffer => buffer.CurrentSnapshot == Mock.Of<ITextSnapshot>() && buffer.Properties == new PropertyCollection() && buffer.ContentType == TestVirtualDocumentFactory.LanguageLSPContentTypeInstance));
            TextBufferFactory = textBufferFactory.Object;

            var hostContentType = Mock.Of<IContentType>(contentType => contentType.IsOfType(TestVirtualDocumentFactory.HostDocumentContentTypeNameConst) == true);
            HostLSPBuffer = Mock.Of<ITextBuffer>(textBuffer => textBuffer.ContentType == hostContentType);

            var nonHostLSPContentType = Mock.Of<IContentType>(contentType => contentType.IsOfType(It.IsAny<string>()) == false);
            NonHostLSPBuffer = Mock.Of<ITextBuffer>(textBuffer => textBuffer.ContentType == nonHostLSPContentType);

            TextDocumentFactoryService = Mock.Of<ITextDocumentFactoryService>();
        }

        private ITextBuffer NonHostLSPBuffer { get; }

        private ITextBuffer HostLSPBuffer { get; }

        private IContentTypeRegistryService ContentTypeRegistry { get; }

        private ITextBufferFactoryService TextBufferFactory { get; }

        private ITextDocumentFactoryService TextDocumentFactoryService { get; }

        [Fact]
        public void TryCreateFor_NonHostLSPBuffer_ReturnsFalse()
        {
            // Arrange
            var uri = new Uri("C:/path/to/file.razor");
            var uriProvider = Mock.Of<FileUriProvider>(provider => provider.GetOrCreate(It.IsAny<ITextBuffer>()) == uri);
            var factory = new TestVirtualDocumentFactory(ContentTypeRegistry, TextBufferFactory, TextDocumentFactoryService, uriProvider);

            // Act
            var result = factory.TryCreateFor(NonHostLSPBuffer, out var virtualDocument);

            // Assert
            Assert.False(result);
            Assert.Null(virtualDocument);
        }

        [Fact]
        public void TryCreateFor_HostLSPBuffer_ReturnsLanguageVirtualDocumentAndTrue()
        {
            // Arrange
            var uri = new Uri("C:/path/to/file.razor");
            var uriProvider = Mock.Of<FileUriProvider>(provider => provider.GetOrCreate(HostLSPBuffer) == uri);
            var factory = new TestVirtualDocumentFactory(ContentTypeRegistry, TextBufferFactory, TextDocumentFactoryService, uriProvider);

            // Act
            var result = factory.TryCreateFor(HostLSPBuffer, out var virtualDocument);

            // Assert
            Assert.True(result);
            Assert.NotNull(virtualDocument);
            Assert.EndsWith(TestVirtualDocumentFactory.LanguageFileNameSuffixConst, virtualDocument.Uri.OriginalString, StringComparison.Ordinal);
            Assert.Equal(TestVirtualDocumentFactory.LanguageLSPContentTypeInstance, virtualDocument.TextBuffer.ContentType);
        }
    }
}
