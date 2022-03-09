// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage.Extensions
{
    internal static class TextBufferExtensions
    {
        private const string HostDocumentVersionMarked = "__MsLsp_HostDocumentVersionMarker__";

        public static void SetHostDocumentSyncVersion(this ITextBuffer textBuffer!!, long hostDocumentVersion)
        {
            textBuffer.Properties[HostDocumentVersionMarked] = hostDocumentVersion;
        }

        public static bool TryGetHostDocumentSyncVersion(this ITextBuffer textBuffer!!, out long hostDocumentVersion)
        {
            var result = textBuffer.Properties.TryGetProperty(HostDocumentVersionMarked, out hostDocumentVersion);

            return result;
        }
    }
}
