using System;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage.Test;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    internal class TestVirtualDocumentFactory : VirtualDocumentFactoryBase
    {
        public const string HostDocumentContentTypeNameConst = "TestHostContentTypeName";
        public const string LanguageContentTypeNameConst = "TestLanguageContentTypeName";
        public const string LanguageFileNameSuffixConst = "__virtual.test";

        public static IContentType LanguageLSPContentTypeInstance { get; } = new TestContentType(LanguageContentTypeNameConst);

        public TestVirtualDocumentFactory(
            IContentTypeRegistryService contentTypeRegistryService,
            ITextBufferFactoryService textBufferFactoryService,
            ITextDocumentFactoryService textDocumentFactoryService,
            FileUriProvider fileUriProvider
            ) : base(contentTypeRegistryService, textBufferFactoryService, textDocumentFactoryService, fileUriProvider) { }

        protected override IContentType LanguageLSPContentType => LanguageLSPContentTypeInstance;

        protected override string LanguageFileNameSuffix => LanguageFileNameSuffixConst;

        protected override string HostDocumentContentTypeName => HostDocumentContentTypeNameConst;

        protected override VirtualDocument CreateVirtualDocument(Uri uri, ITextBuffer textBuffer) => new TestVirtualDocument(uri, textBuffer);
    }
}
