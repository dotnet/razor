// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal interface IRazorDocumentMappingService
{
    TextEdit[] GetHostDocumentEdits(IRazorGeneratedDocument generatedDocument, TextEdit[] generatedDocumentEdits);

    bool TryMapToHostDocumentRange(IRazorGeneratedDocument generatedDocument, Range generatedDocumentRange, MappingBehavior mappingBehavior, [NotNullWhen(true)] out Range? hostDocumentRange);

    bool TryMapToProjectedDocumentRange(IRazorGeneratedDocument generatedDocument, Range originalRange, [NotNullWhen(true)] out Range? projectedRange);

    bool TryMapToHostDocumentPosition(IRazorGeneratedDocument generatedDocument, int generatedDocumentIndex, [NotNullWhen(true)] out Position? hostDocumentPosition, out int hostDocumentIndex);

    bool TryMapToProjectedDocumentPosition(IRazorGeneratedDocument generatedDocument, int absoluteIndex, [NotNullWhen(true)] out Position? projectedPosition, out int projectedIndex);

    bool TryMapToProjectedDocumentOrNextCSharpPosition(IRazorGeneratedDocument generatedDocument, int absoluteIndex, [NotNullWhen(true)] out Position? projectedPosition, out int projectedIndex);

    RazorLanguageKind GetLanguageKind(RazorCodeDocument codeDocument, int originalIndex, bool rightAssociative);

    Task<WorkspaceEdit> RemapWorkspaceEditAsync(WorkspaceEdit workspaceEdit, CancellationToken cancellationToken);

    /// <summary>
    /// Maps a range in the specified virtual document uri to a range in the Razor document that owns the
    /// virtual document. If the uri passed in is not for a virtual document, or the range cannot be mapped
    /// for some other reason, the original passed in range is returned unchanged.
    /// </summary>
    Task<(Uri MappedDocumentUri, Range MappedRange)> MapFromProjectedDocumentRangeAsync(Uri virtualDocumentUri, Range projectedRange, CancellationToken cancellationToken);
}

internal record Projection(RazorLanguageKind LanguageKind, Position Position, int AbsoluteIndex);
