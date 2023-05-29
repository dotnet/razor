// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal abstract class IRazorDocumentMappingService
{
    public abstract TextEdit[] GetProjectedDocumentEdits(IRazorGeneratedDocument generatedDocument, TextEdit[] edits);

    public abstract bool TryMapFromProjectedDocumentRange(IRazorGeneratedDocument generatedDocument, Range projectedRange, [NotNullWhen(true)] out Range? originalRange);

    public abstract bool TryMapFromProjectedDocumentRange(IRazorGeneratedDocument generatedDocument, Range projectedRange, MappingBehavior mappingBehavior, [NotNullWhen(true)] out Range? originalRange);

    public abstract bool TryMapToProjectedDocumentRange(IRazorGeneratedDocument generatedDocument, Range originalRange, [NotNullWhen(true)] out Range? projectedRange);

    public abstract bool TryMapFromProjectedDocumentPosition(IRazorGeneratedDocument generatedDocument, int csharpAbsoluteIndex, [NotNullWhen(true)] out Position? originalPosition, out int originalIndex);

    public abstract bool TryMapToProjectedDocumentPosition(IRazorGeneratedDocument generatedDocument, int absoluteIndex, [NotNullWhen(true)] out Position? projectedPosition, out int projectedIndex);

    public abstract bool TryMapToProjectedDocumentOrNextCSharpPosition(IRazorGeneratedDocument generatedDocument, int absoluteIndex, [NotNullWhen(true)] out Position? projectedPosition, out int projectedIndex);

    public abstract RazorLanguageKind GetLanguageKind(RazorCodeDocument codeDocument, int originalIndex, bool rightAssociative);

    public abstract Task<WorkspaceEdit> RemapWorkspaceEditAsync(WorkspaceEdit workspaceEdit, CancellationToken cancellationToken);

    /// <summary>
    /// Maps a range in the specified virtual document uri to a range in the Razor document that owns the
    /// virtual document. If the uri passed in is not for a virtual document, or the range cannot be mapped
    /// for some other reason, the original passed in range is returned unchanged.
    /// </summary>
    public abstract Task<(Uri MappedDocumentUri, Range MappedRange)> MapFromProjectedDocumentRangeAsync(Uri virtualDocumentUri, Range projectedRange, CancellationToken cancellationToken);
}

internal record Projection(RazorLanguageKind LanguageKind, Position Position, int AbsoluteIndex);
