// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

internal class RazorSemanticTokensInfoService : IRazorSemanticTokensInfoService
{
    private const int TokenSize = 5;

    private readonly IRazorDocumentMappingService _documentMappingService;
    private readonly RazorLSPOptionsMonitor _razorLSPOptionsMonitor;
    private readonly ClientNotifierServiceBase _languageServer;
    private readonly ILogger _logger;

    public RazorSemanticTokensInfoService(
        ClientNotifierServiceBase languageServer,
        IRazorDocumentMappingService documentMappingService,
        RazorLSPOptionsMonitor razorLSPOptionsMonitor,
        ILoggerFactory loggerFactory)
    {
        _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
        _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        _razorLSPOptionsMonitor = razorLSPOptionsMonitor ?? throw new ArgumentNullException(nameof(razorLSPOptionsMonitor));

        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _logger = loggerFactory.CreateLogger<RazorSemanticTokensInfoService>();
    }

    public async Task<SemanticTokens?> GetSemanticTokensAsync(
        TextDocumentIdentifier textDocumentIdentifier,
        Range range,
        VersionedDocumentContext documentContext,
        RazorSemanticTokensLegend razorSemanticTokensLegend,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        var razorSemanticRanges = TagHelperSemanticRangeVisitor.VisitAllNodes(codeDocument, range, razorSemanticTokensLegend, _razorLSPOptionsMonitor.CurrentValue.ColorBackground);
        List<SemanticRange>? csharpSemanticRanges = null;

        try
        {
            csharpSemanticRanges = await GetCSharpSemanticRangesAsync(codeDocument, textDocumentIdentifier, range, razorSemanticTokensLegend, documentContext.Version, correlationId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error thrown while retrieving CSharp semantic range.");
        }

        var combinedSemanticRanges = CombineSemanticRanges(razorSemanticRanges, csharpSemanticRanges);

        // We return null when we have an incomplete view of the document.
        // Likely CSharp ahead of us in terms of document versions.
        // We return null (which to the LSP is a no-op) to prevent flashing of CSharp elements.
        if (combinedSemanticRanges is null)
        {
            _logger.LogWarning("Incomplete view of document. C# may be ahead of us in document versions.");
            return null;
        }

        var data = ConvertSemanticRangesToSemanticTokensData(combinedSemanticRanges, codeDocument);
        var tokens = new SemanticTokens { Data = data };

        return tokens;
    }

    private static List<SemanticRange>? CombineSemanticRanges(List<SemanticRange>? ranges1, List<SemanticRange>? ranges2)
    {
        if (ranges1 is null || ranges2 is null)
        {
            // If we have an incomplete view of the situation we should return null so we avoid flashing.
            return null;
        }

        var newList = new List<SemanticRange>(ranges1.Count + ranges2.Count);
        newList.AddRange(ranges1);
        newList.AddRange(ranges2);

        // Because SemanticToken data is generated relative to the previous token it must be in order.
        // We have a guarantee of order within any given language server, but the interweaving of them can be quite complex.
        // Rather than attempting to reason about transition zones we can simply order our ranges since we know there can be no overlapping range.
        newList.Sort();

        return newList;
    }

    // Internal and virtual for testing only
    internal virtual async Task<List<SemanticRange>?> GetCSharpSemanticRangesAsync(
        RazorCodeDocument codeDocument,
        TextDocumentIdentifier textDocumentIdentifier,
        Range razorRange,
        RazorSemanticTokensLegend razorSemanticTokensLegend,
        long documentVersion,
        Guid correlationId,
        CancellationToken cancellationToken,
        string? previousResultId = null)
    {
        // We'll try to call into the mapping service to map to the projected range for us. If that doesn't work,
        // we'll try to find the minimal range ourselves.
        var generatedDocument = codeDocument.GetCSharpDocument();
        if (!_documentMappingService.TryMapToGeneratedDocumentRange(generatedDocument, razorRange, out var csharpRange) &&
            !TryGetMinimalCSharpRange(codeDocument, razorRange, out csharpRange))
        {
            // There's no C# in the range.
            return new List<SemanticRange>();
        }

        var csharpResponse = await GetMatchingCSharpResponseAsync(textDocumentIdentifier, documentVersion, csharpRange, correlationId, cancellationToken).ConfigureAwait(false);

        // Indicates an issue with retrieving the C# response (e.g. no response or C# is out of sync with us).
        // Unrecoverable, return default to indicate no change. We've already queued up a refresh request in
        // `GetMatchingCSharpResponseAsync` that will cause us to retry in a bit.
        if (csharpResponse is null)
        {
            _logger.LogWarning("Issue with retrieving C# response for Razor range: ({startLine},{startChar})-({endLine},{endChar})", razorRange.Start.Line, razorRange.Start.Character, razorRange.End.Line, razorRange.End.Character);
            return null;
        }

        var razorRanges = new List<SemanticRange>();
        var colorBackground = _razorLSPOptionsMonitor.CurrentValue.ColorBackground;
        var textClassification = razorSemanticTokensLegend.MarkupTextLiteral;
        var razorSource = codeDocument.GetSourceText();

        var previousSemanticRange = new SemanticRange(0, 0, 0, 0, 0, modifier: 0, fromRazor: false);
        Range? previousRazorSemanticRange = null;
        var tempRange = new Range()
        {
            End = new Position(0, 0),
            Start = new Position(0, 0)
        };
        for (var i = 0; i < csharpResponse.Length; i += TokenSize)
        {
            var lineDelta = csharpResponse[i];
            var charDelta = csharpResponse[i + 1];
            var length = csharpResponse[i + 2];
            var tokenType = csharpResponse[i + 3];
            var tokenModifiers = csharpResponse[i + 4];

            var semanticRange = CSharpDataToSemanticRange(lineDelta, charDelta, length, tokenType, tokenModifiers, previousSemanticRange);
            tempRange.Start.Character = semanticRange.Range.StartCharacter;
            tempRange.Start.Line = semanticRange.Range.StartLine;
            tempRange.End.Character = semanticRange.Range.EndCharacter;
            tempRange.End.Line = semanticRange.Range.EndLine;
            if (_documentMappingService.TryMapToHostDocumentRange(generatedDocument, tempRange, out var originalRange))
            {
                if (razorRange is null || razorRange.OverlapsWith(originalRange))
                {
                    if (colorBackground)
                    {
                        tokenModifiers |= (int)RazorSemanticTokensLegend.RazorTokenModifiers.razorCode;
                        AddAdditionalCSharpWhitespaceRanges(razorRanges, textClassification, razorSource, previousRazorSemanticRange, new RazorRange()
                        {
                            EndCharacter = originalRange.End.Character,
                            EndLine = originalRange.End.Line,
                            StartCharacter = originalRange.Start.Character,
                            StartLine = originalRange.Start.Line
                        }, _logger);
                    }

                    razorRanges.Add(new SemanticRange(semanticRange.Kind, originalRange, tokenModifiers, fromRazor: false));
                }

                previousRazorSemanticRange = originalRange;
            }

            previousSemanticRange = semanticRange;
        }

        return razorRanges;
    }

    private static void AddAdditionalCSharpWhitespaceRanges(List<SemanticRange> razorRanges, int textClassification, SourceText razorSource, Range? previousRazorSemanticRange, RazorRange originalRange, ILogger logger)
    {
        var startChar = originalRange.StartCharacter;
        var originalRangeStart = new Position(originalRange.StartLine, startChar);
        if (previousRazorSemanticRange is not null &&
            previousRazorSemanticRange.End.Line == originalRange.StartLine &&
            previousRazorSemanticRange.End.Character < startChar &&
            previousRazorSemanticRange.End.TryGetAbsoluteIndex(razorSource, logger, out var previousSpanEndIndex) &&
            ContainsOnlySpacesOrTabs(razorSource, previousSpanEndIndex + 1, startChar - previousRazorSemanticRange.End.Character - 1))
        {
            // we're on the same line as previous, lets extend ours to include whitespace between us and the proceeding range
            var whitespaceRange = new Range
            {
                Start = new Position(originalRange.StartLine, previousRazorSemanticRange.End.Character),
                End = originalRangeStart
            };
            razorRanges.Add(new SemanticRange(textClassification, whitespaceRange, (int)RazorSemanticTokensLegend.RazorTokenModifiers.razorCode, fromRazor: false));
        }
        else if (startChar > 0 &&
            previousRazorSemanticRange?.End.Line != originalRange.StartLine &&
            originalRangeStart.TryGetAbsoluteIndex(razorSource, logger, out var originalRangeStartIndex) &&
            ContainsOnlySpacesOrTabs(razorSource, originalRangeStartIndex - startChar + 1, startChar - 1))
        {
            // We're on a new line, and the start of the line is only whitespace, so give that a background color too
            var whitespaceRange = new Range
            {
                Start = new Position(originalRange.StartLine, 0),
                End = originalRangeStart
            };
            razorRanges.Add(new SemanticRange(textClassification, whitespaceRange, (int)RazorSemanticTokensLegend.RazorTokenModifiers.razorCode, fromRazor: false));
        }
    }

    private static bool ContainsOnlySpacesOrTabs(SourceText razorSource, int startIndex, int count)
    {
        var end = startIndex + count;
        for (var i = startIndex; i < end; i++)
        {
            if (razorSource[i] is not ' ' or '\t')
            {
                return false;
            }
        }

        return true;
    }

    // Internal for testing only
    internal static bool TryGetMinimalCSharpRange(RazorCodeDocument codeDocument, Range razorRange, [NotNullWhen(true)] out Range? csharpRange)
    {
        SourceSpan? minGeneratedSpan = null;
        SourceSpan? maxGeneratedSpan = null;

        var sourceText = codeDocument.GetSourceText();
        var textSpan = razorRange.AsTextSpan(sourceText);
        var csharpDoc = codeDocument.GetCSharpDocument();

        // We want to find the min and max C# source mapping that corresponds with our Razor range.
        foreach (var mapping in csharpDoc.SourceMappings)
        {
            var mappedTextSpan = mapping.OriginalSpan.AsTextSpan();

            if (textSpan.OverlapsWith(mappedTextSpan))
            {
                if (minGeneratedSpan is null || mapping.GeneratedSpan.AbsoluteIndex < minGeneratedSpan.Value.AbsoluteIndex)
                {
                    minGeneratedSpan = mapping.GeneratedSpan;
                }

                var mappingEndIndex = mapping.GeneratedSpan.AbsoluteIndex + mapping.GeneratedSpan.Length;
                if (maxGeneratedSpan is null || mappingEndIndex > maxGeneratedSpan.Value.AbsoluteIndex + maxGeneratedSpan.Value.Length)
                {
                    maxGeneratedSpan = mapping.GeneratedSpan;
                }
            }
        }

        // Create a new projected range based on our calculated min/max source spans.
        if (minGeneratedSpan is not null && maxGeneratedSpan is not null)
        {
            var csharpSourceText = codeDocument.GetCSharpSourceText();
            var startRange = minGeneratedSpan.Value.AsRange(csharpSourceText);
            var endRange = maxGeneratedSpan.Value.AsRange(csharpSourceText);

            csharpRange = new Range { Start = startRange.Start, End = endRange.End };
            Debug.Assert(csharpRange.Start.CompareTo(csharpRange.End) <= 0, "Range.Start should not be larger than Range.End");

            return true;
        }

        csharpRange = null;
        return false;
    }

    private async Task<int[]?> GetMatchingCSharpResponseAsync(
        TextDocumentIdentifier textDocumentIdentifier,
        long documentVersion,
        Range csharpRange,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var parameter = new ProvideSemanticTokensRangeParams(textDocumentIdentifier, documentVersion, csharpRange, correlationId);

        var csharpResponse = await _languageServer.SendRequestAsync<ProvideSemanticTokensRangeParams, ProvideSemanticTokensResponse>(
            CustomMessageNames.RazorProvideSemanticTokensRangeEndpoint,
            parameter,
            cancellationToken).ConfigureAwait(false);

        if (csharpResponse is null)
        {
            // C# isn't ready yet, don't make Razor wait for it. Once C# is ready they'll send a refresh notification.
            return Array.Empty<int>();
        }
        else if (csharpResponse.HostDocumentSyncVersion != null && csharpResponse.HostDocumentSyncVersion != documentVersion)
        {
            // No C# response or C# is out of sync with us. Unrecoverable, return null to indicate no change.
            // Once C# syncs up they'll send a refresh notification.
            _logger.LogWarning("C# is out of sync. We are wanting {documentVersion} but C# is at {csharpResponse.HostDocumentSyncVersion}.", documentVersion, csharpResponse.HostDocumentSyncVersion);
            return null;
        }

        var response = csharpResponse.Tokens ?? Array.Empty<int>();
        return response;
    }

    private static SemanticRange CSharpDataToSemanticRange(
        int lineDelta,
        int charDelta,
        int length,
        int tokenType,
        int tokenModifiers,
        SemanticRange previousSemanticRange)
    {
        var startLine = previousSemanticRange.Range.EndLine + lineDelta;
        var previousEndChar = lineDelta == 0 ? previousSemanticRange.Range.StartCharacter : 0;
        var startCharacter = previousEndChar + charDelta;
        var start = new Position(startLine, startCharacter);

        var endLine = startLine;
        var endCharacter = startCharacter + length;
        var end = new Position(endLine, endCharacter);

        var range = new Range()
        {
            Start = start,
            End = end
        };
        var semanticRange = new SemanticRange(tokenType, range, tokenModifiers, fromRazor: false);

        return semanticRange;
    }

    private static RazorRange EmptyRazorRange = new RazorRange()
    {
        EndCharacter = 0,
        EndLine = 0,
        StartCharacter = 0,
        StartLine = 0
    };

    private static int[] ConvertSemanticRangesToSemanticTokensData(
        List<SemanticRange> semanticRanges,
        RazorCodeDocument razorCodeDocument)
    {
        var previousResult = EmptyRazorRange;
        var sourceText = razorCodeDocument.GetSourceText();

        // We don't bother filtering out duplicate ranges (eg, where C# and Razor both have opinions), but instead take advantage of
        // our sort algorithm to be correct, so we can skip duplicates here. That means our final array may end up smaller than the
        // expected size, so we have to use a list to build it.
        using var _ = ListPool<int>.GetPooledObject(out var data);
        data.SetCapacityIfLarger(semanticRanges.Count * TokenSize);

        foreach (var result in semanticRanges)
        {
            AppendData(result, previousResult, sourceText, data);

            previousResult = result.Range;
        }

        return data.ToArray();

        // We purposely capture and manipulate the "data" array here to avoid allocation
        static void AppendData(
            SemanticRange currentRange,
            RazorRange previousRange,
            SourceText sourceText,
            List<int> data)
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
            var deltaLine = currentRange.Range.StartLine - previousLineIndex;

            int deltaStart;
            if (previousRange != EmptyRazorRange && previousRange.StartLine == currentRange.Range.StartLine)
            {
                deltaStart = currentRange.Range.StartCharacter - previousRange.StartCharacter;

                // If there is no line delta, no char delta, and this isn't the first range
                // then it means this range overlaps the previous, so we skip it.
                if (deltaStart == 0)
                {
                    return;
                }
            }
            else
            {
                deltaStart = currentRange.Range.StartCharacter;
            }

            data.Add(deltaLine);
            data.Add(deltaStart);

            // length
            var length = GetTextLength(currentRange.Range, sourceText);
            Debug.Assert(length > 0);
            data.Add(length);

            // tokenType
            data.Add(currentRange.Kind);

            // tokenModifiers
            data.Add(currentRange.Modifier);
        }
    }

    private static int GetTextLength(RazorRange range, SourceText sourceText)
    {
        if (sourceText is null)
        {
            throw new ArgumentNullException(nameof(sourceText));
        }

        var start = sourceText.GetAbsolutePosition(range.StartLine, range.StartCharacter);
        var end = sourceText.GetAbsolutePosition(range.EndLine, range.EndCharacter);

        var length = end - start;
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException($"{range} resolved to zero or negative length.");
        }

        return length;
    }
}
