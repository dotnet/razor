using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    internal abstract class VirtualDocumentFactoryBase : VirtualDocumentFactory
    {
        private readonly IContentTypeRegistryService _contentTypeRegistry;
        private readonly ITextBufferFactoryService _textBufferFactory;
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly FileUriProvider _fileUriProvider;
        private IContentType _languageLSPContentType;

        public VirtualDocumentFactoryBase(
            IContentTypeRegistryService contentTypeRegistry,
            ITextBufferFactoryService textBufferFactory,
            ITextDocumentFactoryService textDocumentFactory,
            FileUriProvider filePathProvider)
        {
            if (contentTypeRegistry is null)
            {
                throw new ArgumentNullException(nameof(contentTypeRegistry));
            }

            if (textBufferFactory is null)
            {
                throw new ArgumentNullException(nameof(textBufferFactory));
            }

            if (textDocumentFactory is null)
            {
                throw new ArgumentNullException(nameof(textDocumentFactory));
            }

            if (filePathProvider is null)
            {
                throw new ArgumentNullException(nameof(filePathProvider));
            }

            _contentTypeRegistry = contentTypeRegistry;
            _textBufferFactory = textBufferFactory;
            _textDocumentFactory = textDocumentFactory;
            _fileUriProvider = filePathProvider;
        }

        private IContentType LanguageLSPContentType
        {
            get
            {
                if (_languageLSPContentType == null)
                {
                    var contentType = _contentTypeRegistry.GetContentType(LanguageContentTypeName);
                    _languageLSPContentType = ConvertToLSPContentType(contentType);
                }

                return _languageLSPContentType;
            }
        }

        /// <summary>
        /// Converts registered document content type to one that extends "code-languageserver-base" and
        /// CodeRemoteContentDefinition.CodeRemoteContentTypeName if needed.
        /// </summary>
        /// <param name="contentType">Content type registered with IContentTypeRegistryService</param>
        /// <returns>Corresponding LSP/remote content type</returns>
        protected virtual IContentType ConvertToLSPContentType(IContentType contentType) => contentType;

        public override bool TryCreateFor(ITextBuffer hostDocumentBuffer, out VirtualDocument virtualDocument)
        {
            if (hostDocumentBuffer is null)
            {
                throw new ArgumentNullException(nameof(hostDocumentBuffer));
            }

            if (!hostDocumentBuffer.ContentType.IsOfType(HostDocumentContentTypeName))
            {
                // Another content type we don't care about.
                virtualDocument = null;
                return false;
            }

            var hostDocumentUri = _fileUriProvider.GetOrCreate(hostDocumentBuffer);

            // E.g. Index.cshtml => Index.cshtml__virtual.html (for html), similar for other languages
            var virtualLanguageFilePath = hostDocumentUri.GetAbsoluteOrUNCPath() + LanguageFileNameSuffix;
            var virtualLanguageUri = new Uri(virtualLanguageFilePath);

            var languageBuffer = _textBufferFactory.CreateTextBuffer();
            _fileUriProvider.AddOrUpdate(languageBuffer, virtualLanguageUri);
            languageBuffer.Properties.AddProperty(LSPConstants.ContainedLanguageMarker, true);

            if (!(AdditionalLanguageBufferProperties is null))
            {
                foreach (KeyValuePair<object, object> keyValuePair in AdditionalLanguageBufferProperties)
                {
                    languageBuffer.Properties.AddProperty(keyValuePair.Key, keyValuePair.Value);
                }
            }

            // Create a text document to trigger language server initialization for the contained language.
            _textDocumentFactory.CreateTextDocument(languageBuffer, virtualLanguageFilePath);

            languageBuffer.ChangeContentType(LanguageLSPContentType, editTag: null);

            virtualDocument = CreateVirtualDocument(virtualLanguageUri, languageBuffer);
            return true;
        }

        /// <summary>
        /// Creates and returns specific virtual document instance
        /// </summary>
        /// <param name="uri">Virtual document URI</param>
        /// <param name="textBuffer">Language text buffer</param>
        /// <returns>Language-specific virtual document instance</returns>
        protected abstract VirtualDocument CreateVirtualDocument(Uri uri, ITextBuffer textBuffer);

        /// <summary>
        /// Returns contained language content type name that's registered with IContentTypeRegisteryService
        /// </summary>
        protected abstract string LanguageContentTypeName { get; }

        /// <summary>
        /// Returns contained language uri suffix, e.g. __virtual.html or __virtual.css
        /// </summary>
        protected abstract string LanguageFileNameSuffix { get; }

        /// <summary>
        /// Returns additional properties (if any) to set on the language text buffer prior to language server init
        /// </summary>
        protected virtual Dictionary<object, object> AdditionalLanguageBufferProperties => null;

        /// <summary>
        /// Returns supported host document content type name (i.e. host document content type of the for which this factory can create virtual documents)
        /// </summary>
        protected abstract string HostDocumentContentTypeName { get; }
    }
}
