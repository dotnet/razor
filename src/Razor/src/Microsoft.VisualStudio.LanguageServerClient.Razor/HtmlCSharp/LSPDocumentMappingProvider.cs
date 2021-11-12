﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Protocol;

#nullable enable

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    internal abstract class LSPDocumentMappingProvider
    {
        public abstract Task<RazorMapToDocumentRangesResponse?> MapToDocumentRangesAsync(RazorLanguageKind languageKind, Uri razorDocumentUri, Range[] projectedRanges, CancellationToken cancellationToken);

        public abstract Task<RazorMapToDocumentRangesResponse?> MapToDocumentRangesAsync(RazorLanguageKind languageKind, Uri razorDocumentUri, Range[] projectedRanges, LanguageServerMappingBehavior mappingBehavior, CancellationToken cancellationToken);

        public abstract Task<Location[]> RemapLocationsAsync(Location[] locations, CancellationToken cancellationToken);

        public abstract Task<TextEdit[]> RemapTextEditsAsync(Uri uri, TextEdit[] edits, CancellationToken cancellationToken);

        public abstract Task<TextEdit[]> RemapFormattedTextEditsAsync(Uri uri, TextEdit[] edits, FormattingOptions options, bool containsSnippet, CancellationToken cancellationToken);

        public abstract Task<WorkspaceEdit> RemapWorkspaceEditAsync(WorkspaceEdit workspaceEdit, CancellationToken cancellationToken);
    }
}
