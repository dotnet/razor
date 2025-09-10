﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.CodeAnalysis.Razor.SemanticTokens;

internal abstract class AbstractRazorSemanticTokensInfoService(
    IDocumentMappingService documentMappingService,
    ISemanticTokensLegendService semanticTokensLegendService,
    ICSharpSemanticTokensProvider csharpSemanticTokensProvider,
    ILogger logger)
    : IRazorSemanticTokensInfoService
{
    private const int TokenSize = 5;

    // Use a custom pool as these lists commonly exceed the size threshold for returning into the default ListPool.
    private static readonly ObjectPool<List<SemanticRange>> s_pool = DefaultPool.Create(Policy.Instance, size: 8);

    private readonly IDocumentMappingService _documentMappingService = documentMappingService;
    private readonly ISemanticTokensLegendService _semanticTokensLegendService = semanticTokensLegendService;
    private readonly ICSharpSemanticTokensProvider _csharpSemanticTokensProvider = csharpSemanticTokensProvider;
    private readonly ILogger _logger = logger;

    public async Task<int[]?> GetSemanticTokensAsync(
        DocumentContext documentContext,
        LinePositionSpan span,
        bool colorBackground,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var semanticTokens = await GetSemanticTokensAsync(documentContext, span, correlationId, colorBackground, cancellationToken).ConfigureAwait(false);

        var amount = semanticTokens is null ? "no" : (semanticTokens.Length / TokenSize).ToString(Thread.CurrentThread.CurrentCulture);

        _logger.LogDebug($"Returned {amount} semantic tokens for span {span} in {documentContext.Uri}.");

        if (semanticTokens is not null)
        {
            Debug.Assert(semanticTokens.Length % TokenSize == 0, $"Number of semantic token-ints should be divisible by {TokenSize}. Actual number: {semanticTokens.Length}");
            Debug.Assert(semanticTokens.Length == 0 || semanticTokens[0] >= 0, $"Line offset should not be negative.");
        }

        return semanticTokens;
    }

    private async Task<int[]?> GetSemanticTokensAsync(
        DocumentContext documentContext,
        LinePositionSpan span,
        Guid correlationId,
        bool colorBackground,
        CancellationToken cancellationToken)
    {
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var textSpan = codeDocument.Source.Text.GetTextSpan(span);
        var combinedSemanticRanges = s_pool.Get();

        SemanticTokensVisitor.AddSemanticRanges(combinedSemanticRanges, codeDocument, textSpan, _semanticTokensLegendService, colorBackground);
        Debug.Assert(combinedSemanticRanges.SequenceEqual(combinedSemanticRanges.OrderBy(g => g)));

        var successfullyRetrievedCSharpSemanticRanges = false;

        try
        {
            successfullyRetrievedCSharpSemanticRanges = await AddCSharpSemanticRangesAsync(combinedSemanticRanges, documentContext, codeDocument, span, colorBackground, correlationId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error thrown while retrieving CSharp semantic range.");
        }

        // Didn't get any C# tokens, likely because the user kept typing and a future semantic tokens request will occur.
        // We return null (which to the LSP is a no-op) to prevent flashing of CSharp elements.
        if (!successfullyRetrievedCSharpSemanticRanges)
        {
            _logger.LogDebug($"Couldn't get C# tokens for version {documentContext.Snapshot.Version} of {documentContext.Uri}. Returning null");
            return null;
        }

        // If we have both types of tokens then we need to sort them all together, even though we know the Razor ranges will be sorted already,
        // because they can arbitrarily interleave. The SemanticRange.CompareTo method also has some logic to ensure that if Razor and C# ranges
        // are equivalent, the Razor range will be ordered first, so we can later drop the C# range, and prefer our classification over C#s.
        // Additionally, as mentioned above, the C# ranges are not guaranteed to be in order
        combinedSemanticRanges.Sort();

        var semanticTokens = ConvertSemanticRangesToSemanticTokensData(combinedSemanticRanges, codeDocument);

        s_pool.Return(combinedSemanticRanges);

        return semanticTokens;
    }

    // Virtual for benchmarks
    protected virtual async Task<bool> AddCSharpSemanticRangesAsync(
        List<SemanticRange> ranges,
        DocumentContext documentContext,
        RazorCodeDocument codeDocument,
        LinePositionSpan razorSpan,
        bool colorBackground,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var generatedDocument = codeDocument.GetRequiredCSharpDocument();

        // Get a list of precise ranges for the C# code embedded in the Razor document.
        if (!TryGetSortedCSharpRanges(codeDocument, razorSpan, out var csharpRanges))
        {
            // There's no C# in the range.
            return true;
        }

        _logger.LogDebug($"Requesting C# semantic tokens for host version {documentContext.Snapshot.Version}, correlation ID {correlationId}, and the server thinks there are {codeDocument.GetCSharpSourceText().Lines.Count} lines of C#");

        var csharpResponse = await _csharpSemanticTokensProvider.GetCSharpSemanticTokensResponseAsync(documentContext, csharpRanges, correlationId, cancellationToken).ConfigureAwait(false);

        // Indicates an issue with retrieving the C# response (e.g. no response or C# is out of sync with us).
        // Unrecoverable, return default to indicate no change. We've already queued up a refresh request in
        // the server call that will cause us to retry in a bit.
        if (csharpResponse is null)
        {
            return false;
        }

        ranges.SetCapacityIfLarger(csharpResponse.Length / TokenSize);

        var textClassification = _semanticTokensLegendService.TokenTypes.MarkupTextLiteral;
        var razorSource = codeDocument.Source.Text;

        SemanticRange previousSemanticRange = default;
        LinePositionSpan? previousRazorSemanticRange = null;

        for (var i = 0; i < csharpResponse.Length; i += TokenSize)
        {
            var lineDelta = csharpResponse[i];
            var charDelta = csharpResponse[i + 1];
            var length = csharpResponse[i + 2];
            var tokenType = csharpResponse[i + 3];
            var tokenModifiers = csharpResponse[i + 4];

            var semanticRange = CSharpDataToSemanticRange(lineDelta, charDelta, length, tokenType, tokenModifiers, previousSemanticRange);
            if (_documentMappingService.TryMapToRazorDocumentRange(generatedDocument, semanticRange.AsLinePositionSpan(), out var originalRange))
            {
                if (razorSpan.OverlapsWith(originalRange))
                {
                    if (colorBackground)
                    {
                        tokenModifiers |= _semanticTokensLegendService.TokenModifiers.RazorCodeModifier;
                        AddAdditionalCSharpWhitespaceRanges(ranges, textClassification, razorSource, previousRazorSemanticRange, originalRange);
                    }

                    ranges.Add(new SemanticRange(semanticRange.Kind, originalRange.Start.Line, originalRange.Start.Character, originalRange.End.Line, originalRange.End.Character, tokenModifiers, fromRazor: false));
                }

                previousRazorSemanticRange = originalRange;
            }

            previousSemanticRange = semanticRange;
        }

        return true;
    }

    private void AddAdditionalCSharpWhitespaceRanges(List<SemanticRange> razorRanges, int textClassification, SourceText razorSource, LinePositionSpan? previousRazorSemanticRange, LinePositionSpan originalRange)
    {
        var startLine = originalRange.Start.Line;
        var startChar = originalRange.Start.Character;
        if (previousRazorSemanticRange is { } previousRange &&
            previousRange.End.Line == startLine &&
            previousRange.End.Character < startChar &&
            razorSource.TryGetAbsoluteIndex(previousRange.End, out var previousSpanEndIndex) &&
            ContainsOnlySpacesOrTabs(razorSource, previousSpanEndIndex + 1, startChar - previousRange.End.Character - 1))
        {
            // we're on the same line as previous, lets extend ours to include whitespace between us and the proceeding range
            razorRanges.Add(new SemanticRange(textClassification, startLine, previousRange.End.Character, startLine, startChar, _semanticTokensLegendService.TokenModifiers.RazorCodeModifier, fromRazor: false));
        }
        else if (startChar > 0 &&
            previousRazorSemanticRange?.End.Line != startLine &&
            razorSource.TryGetAbsoluteIndex(originalRange.Start, out var originalRangeStartIndex) &&
            ContainsOnlySpacesOrTabs(razorSource, originalRangeStartIndex - startChar + 1, startChar - 1))
        {
            // We're on a new line, and the start of the line is only whitespace, so give that a background color too
            razorRanges.Add(new SemanticRange(textClassification, startLine, 0, startLine, startChar, _semanticTokensLegendService.TokenModifiers.RazorCodeModifier, fromRazor: false));
        }
    }

    private static bool ContainsOnlySpacesOrTabs(SourceText razorSource, int startIndex, int count)
    {
        var end = startIndex + count;
        for (var i = startIndex; i < end; i++)
        {
            if (razorSource[i] is not (' ' or '\t'))
            {
                return false;
            }
        }

        return true;
    }

    // Internal for testing only
    internal static bool TryGetSortedCSharpRanges(RazorCodeDocument codeDocument, LinePositionSpan razorRange, out ImmutableArray<LinePositionSpan> ranges)
    {
        using var _ = ArrayBuilderPool<LinePositionSpan>.GetPooledObject(out var csharpRanges);
        var csharpSourceText = codeDocument.GetCSharpSourceText();
        var sourceText = codeDocument.Source.Text;
        var textSpan = sourceText.GetTextSpan(razorRange);
        var csharpDoc = codeDocument.GetRequiredCSharpDocument();

        // We want to find the min and max C# source mapping that corresponds with our Razor range.
        foreach (var mapping in csharpDoc.SourceMappings)
        {
            var mappedTextSpan = mapping.OriginalSpan.AsTextSpan();

            if (textSpan.OverlapsWith(mappedTextSpan))
            {
                var mappedRange = csharpSourceText.GetLinePositionSpan(mapping.GeneratedSpan);
                csharpRanges.Add(mappedRange);
            }
        }

        if (csharpRanges.Count == 0)
        {
            ranges = [];
            return false;
        }

        csharpRanges.Sort(CompareLinePositionSpans);
        ranges = csharpRanges.ToImmutableAndClear();
        return true;
    }

    private static int CompareLinePositionSpans(LinePositionSpan span1, LinePositionSpan span2)
    {
        var result = span1.Start.CompareTo(span2.Start);

        if (result == 0)
        {
            result = span1.End.CompareTo(span2.End);
        }

        return result;
    }

    private static SemanticRange CSharpDataToSemanticRange(
        int lineDelta,
        int charDelta,
        int length,
        int tokenType,
        int tokenModifiers,
        SemanticRange previousSemanticRange)
    {
        var startLine = previousSemanticRange.EndLine + lineDelta;
        var previousEndChar = lineDelta == 0 ? previousSemanticRange.StartCharacter : 0;
        var startCharacter = previousEndChar + charDelta;

        var endLine = startLine;
        var endCharacter = startCharacter + length;

        var semanticRange = new SemanticRange(tokenType, startLine, startCharacter, endLine, endCharacter, tokenModifiers, fromRazor: false);

        return semanticRange;
    }

    private static int[] ConvertSemanticRangesToSemanticTokensData(
        List<SemanticRange> semanticRanges,
        RazorCodeDocument razorCodeDocument)
    {
        SemanticRange previousResult = default;

        var sourceText = razorCodeDocument.Source.Text;

        // We don't bother filtering out duplicate ranges (eg, where C# and Razor both have opinions), but instead take advantage of
        // our sort algorithm to be correct, so we can skip duplicates here. That means our final array may end up smaller than the
        // expected size.
        var data = new int[semanticRanges.Count * TokenSize];

        var firstRange = true;
        var index = 0;
        foreach (var result in semanticRanges)
        {
            ConvertIntoDataArray(result, previousResult, firstRange, sourceText, data, ref index);
            firstRange = false;

            previousResult = result;
        }

        // The common case is that the ConvertIntoDataArray calls didn't find any overlap, and we can just directly use the
        // data array we allocated. If there was overlap, then we need to allocate a smaller array and copy the data over.
        if (index == data.Length)
        {
            return data;
        }

        var subset = new int[index];
        Array.Copy(data, subset, index);
        return subset;

        // We purposely capture and manipulate the "data" array here to avoid allocation
        static void ConvertIntoDataArray(
            SemanticRange currentRange,
            SemanticRange previousRange,
            bool firstRange,
            SourceText sourceText,
            int[] data,
            ref int index)
        {
            /*
             * In short, each token takes 5 integers to represent, so a specific token `i` in the file consists of the following array indices:
             *  - at index `5*i`   - `deltaLine`: token line number, relative to the previous token
             *  - at index `5*i+1` - `deltaStart`: token start character, relative to the previous token (relative to 0 or the previous token's start if they are on the same line)
             *  - at index `5*i+2` - `length`: the length of the token. A token cannot be multiline.
             *  - at index `5*i+3` - `tokenType`: will be looked up in `SemanticTokensLegend.tokenTypes`
             *  - at index `5*i+4` - `tokenModifiers`: each set bit will be looked up in `SemanticTokensLegend.tokenModifiers`
            */

            // deltaLine
            var previousLineIndex = previousRange.StartLine;
            var deltaLine = currentRange.StartLine - previousLineIndex;

            int deltaStart;
            if (!firstRange && previousRange.StartLine == currentRange.StartLine)
            {
                deltaStart = currentRange.StartCharacter - previousRange.StartCharacter;

                // If there is no line delta, no char delta, and this isn't the first range
                // then it means this range overlaps the previous, so we skip it.
                if (deltaStart == 0)
                {
                    return;
                }
            }
            else
            {
                deltaStart = currentRange.StartCharacter;
            }

            data[index] = deltaLine;
            data[index + 1] = deltaStart;

            // length

            if (!sourceText.TryGetAbsoluteIndex(currentRange.StartLine, currentRange.StartCharacter, out var startPosition) ||
                !sourceText.TryGetAbsoluteIndex(currentRange.EndLine, currentRange.EndCharacter, out var endPosition))
            {
                throw new ArgumentOutOfRangeException($"Range: All or part of {currentRange} was outside the bounds of the document.");
            }

            var length = endPosition - startPosition;
            Debug.Assert(length > 0);
            data[index + 2] = length;

            // tokenType
            data[index + 3] = currentRange.Kind;

            // tokenModifiers
            data[index + 4] = currentRange.Modifier;

            index += 5;
        }
    }

    private sealed class Policy : IPooledObjectPolicy<List<SemanticRange>>
    {
        public static readonly Policy Instance = new();

        // Significantly larger than DefaultPool.MaximumObjectSize as these arrays are commonly large.
        // The 2048 limit should be large enough for nearly all semantic token requests, while still
        // keeping the backing arrays off the LOH.
        public const int MaximumObjectSize = 2048;

        private Policy()
        {
        }

        public List<SemanticRange> Create() => new();

        public bool Return(List<SemanticRange> list)
        {
            var count = list.Count;

            list.Clear();

            if (count > MaximumObjectSize)
            {
                list.TrimExcess();
            }

            return true;
        }
    }
}
