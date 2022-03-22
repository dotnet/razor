// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Composition;
using System.IO;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    [Shared]
    [Export(typeof(FileUriProvider))]
    internal class DefaultFileUriProvider : FileUriProvider
    {
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private const string TextBufferUri = "__MsLspTextBufferUri";

        [ImportingConstructor]
        public DefaultFileUriProvider(ITextDocumentFactoryService textDocumentFactory!!)
        {
            _textDocumentFactory = textDocumentFactory;
        }

        public override void AddOrUpdate(ITextBuffer textBuffer!!, Uri uri!!)
        {
            textBuffer.Properties[TextBufferUri] = uri;
        }

        public override Uri GetOrCreate(ITextBuffer textBuffer!!)
        {
            if (TryGet(textBuffer, out var uri))
            {
                return uri;
            }

            string filePath;
            if (_textDocumentFactory.TryGetTextDocument(textBuffer, out var textDocument))
            {
                filePath = textDocument.FilePath;
            }
            else
            {
                // TextBuffer doesn't have a file path, we need to fabricate one.
                filePath = Path.GetTempFileName();
            }

            uri = new Uri(filePath, UriKind.Absolute);
            AddOrUpdate(textBuffer, uri);
            return uri;
        }

        public override bool TryGet(ITextBuffer textBuffer!!, out Uri uri)
        {
            if (textBuffer.Properties.TryGetProperty(TextBufferUri, out uri))
            {
                return true;
            }

            return false;
        }

        public override void Remove(ITextBuffer textBuffer!!)
        {
            textBuffer.Properties.RemoveProperty(TextBufferUri);
        }
    }
}
