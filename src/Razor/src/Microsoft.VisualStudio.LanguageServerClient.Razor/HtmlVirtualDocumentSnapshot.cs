// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal class HtmlVirtualDocumentSnapshot : VirtualDocumentSnapshot
    {
        public HtmlVirtualDocumentSnapshot(
            Uri uri!!,
            ITextSnapshot snapshot!!,
            long? hostDocumentSyncVersion)
        {
            Uri = uri;
            Snapshot = snapshot;
            HostDocumentSyncVersion = hostDocumentSyncVersion;
        }

        public override Uri Uri { get; }

        public override ITextSnapshot Snapshot { get; }

        public override long? HostDocumentSyncVersion { get; }
    }
}
