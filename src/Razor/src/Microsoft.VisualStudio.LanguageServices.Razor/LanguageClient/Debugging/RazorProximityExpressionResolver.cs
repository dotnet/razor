﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Razor.Debugging;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Debugging;

[Export(typeof(IRazorProximityExpressionResolver))]
[method: ImportingConstructor]
internal class RazorProximityExpressionResolver(
    FileUriProvider fileUriProvider,
    LSPDocumentManager documentManager,
    ILSPProximityExpressionsProvider proximityExpressionsProvider) : IRazorProximityExpressionResolver
{
    private record CacheKey(Uri DocumentUri, long? HostDocumentSyncVersion, int Line, int Character);

    private readonly FileUriProvider _fileUriProvider = fileUriProvider;
    private readonly LSPDocumentManager _documentManager = documentManager;
    private readonly ILSPProximityExpressionsProvider _proximityExpressionsProvider = proximityExpressionsProvider;

    // 10 is a magic number where this effectively represents our ability to cache the last 10 "hit" breakpoint locations
    // corresponding proximity expressions which enables us not to go "async" in those re-hit scenarios.
    private readonly MemoryCache<CacheKey, IReadOnlyList<string>> _cache = new(sizeLimit: 10);

    public async Task<IReadOnlyList<string>?> TryResolveProximityExpressionsAsync(ITextBuffer textBuffer, int lineIndex, int characterIndex, CancellationToken cancellationToken)
    {
        if (!_fileUriProvider.TryGet(textBuffer, out var documentUri))
        {
            // Not an addressable Razor document. Do not allow expression resolution here. In practice this shouldn't happen, just being defensive.
            return null;
        }

        if (!_documentManager.TryGetDocument(documentUri, out var documentSnapshot))
        {
            // No associated Razor document. Do not resolve expressions here. In practice this shouldn't happen, just being defensive.
            return null;
        }

        // TODO: Support multiple C# documents per Razor document.
        if (!documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var virtualDocument) ||
            virtualDocument.HostDocumentSyncVersion is not { } hostDocumentSyncVersion)
        {
            Debug.Fail($"Some how there's no C# document associated with the host Razor document {documentUri.OriginalString} when resolving proximity expressions.");
            return null;
        }

        var cacheKey = new CacheKey(documentSnapshot.Uri, virtualDocument.HostDocumentSyncVersion, lineIndex, characterIndex);
        if (_cache.TryGetValue(cacheKey, out var cachedExpressions))
        {
            // We've seen this request before, no need to go async.
            return cachedExpressions;
        }

        var position = VsLspFactory.CreatePosition(lineIndex, characterIndex);
        var proximityExpressions = await _proximityExpressionsProvider.GetProximityExpressionsAsync(documentSnapshot, hostDocumentSyncVersion, position, cancellationToken).ConfigureAwait(false);

        // Cache range so if we're asked again for this document/line/character we don't have to go async.
        // Note: If we didn't get any proximity expressions back--likely due to an error--we cache an empty array.
        _cache.Set(cacheKey, proximityExpressions ?? []);

        return proximityExpressions;
    }
}
