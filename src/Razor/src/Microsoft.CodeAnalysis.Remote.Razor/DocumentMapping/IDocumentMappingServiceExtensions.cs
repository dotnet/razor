// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Remote.Razor.DocumentMapping;

internal static class IDocumentMappingServiceExtensions
{
    /// <summary>
    /// Maps a range in the specified generated document uri to a range in the Razor document that owns the
    /// generated document. If the uri passed in is not for a generated document, or the range cannot be mapped
    /// for some other reason, the original passed in range is returned unchanged.
    /// </summary>
    public static Task<(DocumentUri MappedDocumentUri, LinePositionSpan MappedRange)> MapToHostDocumentUriAndRangeAsync(
        this IDocumentMappingService service,
        RemoteDocumentSnapshot originSnapshot,
        DocumentUri generatedDocumentUri,
        LinePositionSpan generatedDocumentRange,
        CancellationToken cancellationToken)
    {
        if (service is RemoteDocumentMappingService remoteService)
        {
            return remoteService.MapToHostDocumentUriAndRangeAsync(originSnapshot, generatedDocumentUri, generatedDocumentRange, cancellationToken);
        }

        return Assumed.Unreachable<Task<(DocumentUri, LinePositionSpan)>>();
    }

    /// <summary>
    /// Maps a range in the specified generated document uri to a range in the Razor document that owns the
    /// generated document. If the uri passed in is not for a generated document, or the range cannot be mapped
    /// for some other reason, the original passed in range is returned unchanged.
    /// </summary>
    public static async Task<(DocumentUri MappedDocumentUri, LspRange MappedRange)> MapToHostDocumentUriAndRangeAsync(
        this IDocumentMappingService service,
        RemoteDocumentSnapshot originSnapshot,
        DocumentUri generatedDocumentUri,
        LspRange generatedDocumentRange,
        CancellationToken cancellationToken)
    {
        var (mappedDocumentUri, mappedRange) = await service
            .MapToHostDocumentUriAndRangeAsync(
                originSnapshot,
                generatedDocumentUri,
                generatedDocumentRange.ToLinePositionSpan(),
                cancellationToken)
            .ConfigureAwait(false);

        return (mappedDocumentUri, mappedRange.ToRange());
    }
}
