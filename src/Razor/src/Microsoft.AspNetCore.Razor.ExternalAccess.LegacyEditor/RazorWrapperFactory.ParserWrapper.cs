// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.VisualStudio.Editor.Razor;
using DocumentStructureChangedEventArgsInternal = Microsoft.VisualStudio.Editor.Razor.DocumentStructureChangedEventArgs;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.LegacyEditor;

internal static partial class RazorWrapperFactory
{
    private class ParserWrapper(VisualStudioRazorParser parser) : IRazorParser
    {
        private readonly VisualStudioRazorParser _parser = parser;
        private EventHandler<DocumentStructureChangedEventArgs>? _documentStructureChanged;

        public bool HasPendingChanges => _parser.HasPendingChanges;

        public event EventHandler<DocumentStructureChangedEventArgs>? DocumentStructureChanged
        {
            add
            {
                // If this is the first handler, hook the inner event.
                if (_documentStructureChanged is null)
                {
                    _parser.DocumentStructureChanged += OnDocumentStructureChanged;
                }

                _documentStructureChanged += value;
            }

            remove
            {
                _documentStructureChanged -= value;

                // If there are no more handlers, unhook the inner event.
                if (_documentStructureChanged is null)
                {
                    _parser.DocumentStructureChanged -= OnDocumentStructureChanged;
                }
            }
        }

        private void OnDocumentStructureChanged(object sender, DocumentStructureChangedEventArgsInternal e)
        {
            // Be sure to use our wrapper as the sender to avoid leaking the inner object.
            if (_documentStructureChanged is { } handler)
            {
                handler(this, new DocumentStructureChangedEventArgs(
                    ConvertSourceChange(e.SourceChange), e.Snapshot, e.CodeDocument));
            }
        }

        public void QueueReparse() => _parser.QueueReparse();
    }
}
