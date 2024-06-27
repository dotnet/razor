﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.FoldingRanges;

internal class FoldingRangeService(
    IRazorDocumentMappingService documentMappingService,
    IEnumerable<IRazorFoldingRangeProvider> foldingRangeProviders,
    ILoggerFactory loggerFactory)
    : IFoldingRangeService
{
    private readonly IRazorDocumentMappingService _documentMappingService = documentMappingService;
    private readonly IEnumerable<IRazorFoldingRangeProvider> _foldingRangeProviders = foldingRangeProviders;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<FoldingRangeService>();

    public ImmutableArray<FoldingRange> GetFoldingRanges(RazorCodeDocument codeDocument, ImmutableArray<FoldingRange> csharpRanges, ImmutableArray<FoldingRange> htmlRanges, CancellationToken cancellationToken)
    {
        using var _ = ArrayBuilderPool<FoldingRange>.GetPooledObject(out var mappedRanges);

        // We have no idea how many ranges we'll end up with, because we expect to filter out a lot of C# ranges,
        // but we will at least have one per html range so can avoid some initial resizing of the backing data store.
        mappedRanges.SetCapacityIfLarger(htmlRanges.Length);

        var csharpDocument = codeDocument.GetCSharpDocument();

        foreach (var foldingRange in csharpRanges)
        {
            var span = GetLinePositionSpan(foldingRange);

            if (_documentMappingService.TryMapToHostDocumentRange(csharpDocument, span, out var mappedSpan))
            {
                foldingRange.StartLine = mappedSpan.Start.Line;
                foldingRange.StartCharacter = mappedSpan.Start.Character;
                foldingRange.EndLine = mappedSpan.End.Line;
                foldingRange.EndCharacter = mappedSpan.End.Character;

                mappedRanges.Add(foldingRange);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Html ranges don't need mapping. Yay!
        mappedRanges.AddRange(htmlRanges);

        foreach (var provider in _foldingRangeProviders)
        {
            var ranges = provider.GetFoldingRanges(codeDocument);
            mappedRanges.AddRange(ranges);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Don't allow ranges to be reported if they aren't spanning at least one line
        var validRanges = mappedRanges.Where(r => r.StartLine < r.EndLine);

        // Reduce ranges that have the same start line to be a single instance with the largest
        // range available, since only one button can be shown to collapse per line
        var reducedRanges = validRanges
            .GroupBy(r => r.StartLine)
            .Select(ranges => ranges.OrderByDescending(r => r.EndLine).First());

        // Fix the starting range so the "..." is shown at the end
        return reducedRanges.SelectAsArray(r => FixFoldingRangeStart(r, codeDocument));
    }

    /// <summary>
    /// Fixes the start of a range so that the offset of the first line is the last character on that line. This makes
    /// it so collapsing will still show the text instead of just "..."
    /// </summary>
    private FoldingRange FixFoldingRangeStart(FoldingRange range, RazorCodeDocument codeDocument)
    {
        Debug.Assert(range.StartLine < range.EndLine);

        var sourceText = codeDocument.GetSourceText();
        var startLine = range.StartLine;

        if (startLine >= sourceText.Lines.Count)
        {
            // Sometimes VS Code seems to send us wildly out-of-range folding ranges for Html, so log a warning,
            // but prevent a toast from appearing from an exception.
            _logger.LogWarning($"Got a folding range of ({range.StartLine}-{range.EndLine}) but Razor document {codeDocument.Source.FilePath} only has {sourceText.Lines.Count} lines.");
            return range;
        }

        var lineSpan = sourceText.Lines[startLine].Span;

        // Search from the end of the line to the beginning for the first non whitespace character. We want that
        // to be the offset for the range
        var offset = sourceText.GetLastNonWhitespaceOffset(lineSpan, out _);

        if (offset.HasValue)
        {
            // +1 to the offset value because the helper goes to the character position
            // that we want to be after. Make sure we don't exceed the line end
            var newCharacter = Math.Min(offset.Value + 1, lineSpan.Length);

            range.StartCharacter = newCharacter;
            range.CollapsedText = null; // Let the client decide what to show
            return range;
        }

        return range;
    }

    private static LinePositionSpan GetLinePositionSpan(FoldingRange foldingRange)
        => new(new(foldingRange.StartLine, foldingRange.StartCharacter.GetValueOrDefault()), new(foldingRange.EndLine, foldingRange.EndCharacter.GetValueOrDefault()));
}
