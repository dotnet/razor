// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Export(typeof(VirtualDocumentFactory))]
    [ContentType(RazorLSPConstants.RazorLSPContentTypeName)]
    internal class HtmlVirtualDocumentFactory : VirtualDocumentFactoryBase
    {
        private static IContentType s_htmlLSPContentType;

        [ImportingConstructor]
        public HtmlVirtualDocumentFactory(
            IContentTypeRegistryService contentTypeRegistry,
            ITextBufferFactoryService textBufferFactory,
            ITextDocumentFactoryService textDocumentFactory,
            FileUriProvider filePathProvider)
            : base(contentTypeRegistry, textBufferFactory, textDocumentFactory, filePathProvider)
        {
        }

        protected override IContentType LanguageContentType
        {
            get
            {
                if (s_htmlLSPContentType == null)
                {
                    s_htmlLSPContentType = ContentTypeRegistry.GetContentType(RazorLSPConstants.HtmlLSPDelegationContentTypeName);
                }

                return s_htmlLSPContentType;
            }
        }

        protected override string HostDocumentContentTypeName => RazorLSPConstants.RazorLSPContentTypeName;
        protected override string LanguageFileNameSuffix => RazorLSPConstants.VirtualHtmlFileNameSuffix;
        protected override VirtualDocument CreateVirtualDocument(Uri uri, ITextBuffer textBuffer) => new HtmlVirtualDocument(uri, textBuffer);
    }
}
