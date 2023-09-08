﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

internal class RazorSemanticTokensInfoService : IRazorSemanticTokensInfoService
{
    private const int TokenSize = 5;

    private readonly IRazorDocumentMappingService _documentMappingService;
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;
    private readonly RazorLSPOptionsMonitor _razorLSPOptionsMonitor;
    private readonly ClientNotifierServiceBase _languageServer;
    private readonly ILogger _logger;

    public RazorSemanticTokensInfoService(
        ClientNotifierServiceBase languageServer,
        IRazorDocumentMappingService documentMappingService,
        RazorLSPOptionsMonitor razorLSPOptionsMonitor,
        LanguageServerFeatureOptions languageServerFeatureOptions,
        ILoggerFactory loggerFactory)
    {
        _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
        _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        _razorLSPOptionsMonitor = razorLSPOptionsMonitor ?? throw new ArgumentNullException(nameof(razorLSPOptionsMonitor));
        _languageServerFeatureOptions = languageServerFeatureOptions ?? throw new ArgumentNullException(nameof(languageServerFeatureOptions));

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

        // Didn't get any C# tokens, likely because the user kept typing and a future semantic tokens request will occur.
        // We return null (which to the LSP is a no-op) to prevent flashing of CSharp elements.
        if (csharpSemanticRanges is null)
        {
            _logger.LogDebug("Couldn't get C# tokens for version {version} of {doc}. Returning null", documentContext.Version, textDocumentIdentifier.Uri);
            return null;
        }

        var combinedSemanticRanges = CombineSemanticRanges(razorSemanticRanges, csharpSemanticRanges);

        var data = ConvertSemanticRangesToSemanticTokensData(combinedSemanticRanges, codeDocument);
        var tokens = new SemanticTokens { Data = data };

        return tokens;
    }

    private static List<SemanticRange> CombineSemanticRanges(List<SemanticRange> ranges1, List<SemanticRange> ranges2)
    {
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
        var generatedDocument = codeDocument.GetCSharpDocument();
        Range[]? csharpRanges;

        // When the feature flag is enabled we try to get a list of precise ranges for the C# code embedded in the Razor document.
        // The feature flag allows to make calls to Roslyn using multiple smaller and disjoint ranges of the document
        if (_languageServerFeatureOptions.UsePreciseSemanticTokenRanges)
        {
            if (!TryGetSortedCSharpRanges(codeDocument, razorRange, out csharpRanges))
            {
                // There's no C# in the range.
                return new List<SemanticRange>();
            }
        }
        else
        {
            // When the feature flag is disabled, we fallback to computing a single range for the entire document.
            // This single range is the minimal range that contains all of the C# code in the document.
            // We'll try to call into the mapping service to map to the projected range for us. If that doesn't work,
            // we'll try to find the minimal range ourselves.
            if (!_documentMappingService.TryMapToGeneratedDocumentRange(generatedDocument, razorRange, out var csharpRange) &&
                !TryGetMinimalCSharpRange(codeDocument, razorRange, out csharpRange))
            {
                // There's no C# in the range.
                return new List<SemanticRange>();
            }

            csharpRanges = new Range[] { csharpRange };
        }

        var csharpResponse = await GetMatchingCSharpResponseAsync(textDocumentIdentifier, documentVersion, csharpRanges, correlationId, cancellationToken).ConfigureAwait(false);

        // Indicates an issue with retrieving the C# response (e.g. no response or C# is out of sync with us).
        // Unrecoverable, return default to indicate no change. We've already queued up a refresh request in
        // the server call that will cause us to retry in a bit.
        if (csharpResponse is null)
        {
            return null;
        }

        var razorRanges = new List<SemanticRange>();
        var colorBackground = _razorLSPOptionsMonitor.CurrentValue.ColorBackground;
        var textClassification = razorSemanticTokensLegend.MarkupTextLiteral;
        var razorSource = codeDocument.GetSourceText();

        SemanticRange? previousSemanticRange = null;
        Range? previousRazorSemanticRange = null;
        for (var i = 0; i < csharpResponse.Length; i += TokenSize)
        {
            var lineDelta = csharpResponse[i];
            var charDelta = csharpResponse[i + 1];
            var length = csharpResponse[i + 2];
            var tokenType = csharpResponse[i + 3];
            var tokenModifiers = csharpResponse[i + 4];

            var semanticRange = CSharpDataToSemanticRange(lineDelta, charDelta, length, tokenType, tokenModifiers, previousSemanticRange);
            if (_documentMappingService.TryMapToHostDocumentRange(generatedDocument, semanticRange.Range, out var originalRange))
            {
                if (razorRange is null || razorRange.OverlapsWith(originalRange))
                {
                    if (colorBackground)
                    {
                        tokenModifiers |= (int)RazorSemanticTokensLegend.RazorTokenModifiers.razorCode;
                        AddAdditionalCSharpWhitespaceRanges(razorRanges, textClassification, razorSource, previousRazorSemanticRange, originalRange, _logger);
                    }

                    razorRanges.Add(new SemanticRange(semanticRange.Kind, originalRange, tokenModifiers, fromRazor: false));
                }

                previousRazorSemanticRange = originalRange;
            }

            previousSemanticRange = semanticRange;
        }

        return razorRanges;
    }

    private static void AddAdditionalCSharpWhitespaceRanges(List<SemanticRange> razorRanges, int textClassification, SourceText razorSource, Range? previousRazorSemanticRange, Range originalRange, ILogger logger)
    {
        var startChar = originalRange.Start.Character;
        if (previousRazorSemanticRange is not null &&
            previousRazorSemanticRange.End.Line == originalRange.Start.Line &&
            previousRazorSemanticRange.End.Character < originalRange.Start.Character &&
            previousRazorSemanticRange.End.TryGetAbsoluteIndex(razorSource, logger, out var previousSpanEndIndex) &&
            ContainsOnlySpacesOrTabs(razorSource, previousSpanEndIndex + 1, startChar - previousRazorSemanticRange.End.Character - 1))
        {
            // we're on the same line as previous, lets extend ours to include whitespace between us and the proceeding range
            var whitespaceRange = new Range
            {
                Start = new Position(originalRange.Start.Line, previousRazorSemanticRange.End.Character),
                End = originalRange.Start
            };
            razorRanges.Add(new SemanticRange(textClassification, whitespaceRange, (int)RazorSemanticTokensLegend.RazorTokenModifiers.razorCode, fromRazor: false));
        }
        else if (originalRange.Start.Character > 0 &&
            previousRazorSemanticRange?.End.Line != originalRange.Start.Line &&
            originalRange.Start.TryGetAbsoluteIndex(razorSource, logger, out var originalRangeStartIndex) &&
            ContainsOnlySpacesOrTabs(razorSource, originalRangeStartIndex - startChar + 1, startChar - 1))
        {
            // We're on a new line, and the start of the line is only whitespace, so give that a background color too
            var whitespaceRange = new Range
            {
                Start = new Position(originalRange.Start.Line, 0),
                End = originalRange.Start
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
        Range[] csharpRanges,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var parameter = new ProvideSemanticTokensRangesParams(textDocumentIdentifier, documentVersion, csharpRanges, correlationId);

        var csharpResponse = await _languageServer.SendRequestAsync<ProvideSemanticTokensRangesParams, ProvideSemanticTokensResponse>(
            CustomMessageNames.RazorProvideSemanticTokensRangeEndpoint,
            parameter,
            cancellationToken).ConfigureAwait(false);

        if (csharpResponse is null)
        {
            // C# isn't ready yet, don't make Razor wait for it. Once C# is ready they'll send a refresh notification.
            return Array.Empty<int>();
        }

        var csharpVersion = csharpResponse.HostDocumentSyncVersion;
        if (csharpVersion != documentVersion)
        {
            // No C# response or C# is out of sync with us. Unrecoverable, return null to indicate no change.
            // Once C# syncs up they'll send a refresh notification.
            if (csharpVersion == -1)
            {
                _logger.LogWarning("Didn't get C# tokens because the virtual document wasn't found, or other problem. We were wanting {documentVersion} but C# could not get any version.", documentVersion);
            }
            else if (csharpVersion < documentVersion)
            {
                _logger.LogDebug("Didn't wait for Roslyn to get the C# version we were expecting. We are wanting {documentVersion} but C# is at {csharpVersion}.", documentVersion, csharpVersion);
            }
            else
            {
                _logger.LogWarning("We are behind the C# version which is surprising. Could be an old request that wasn't cancelled, but if not, expect most future requests to fail. We were wanting {documentVersion} but C# is at {csharpVersion}.", documentVersion, csharpVersion);
            }

            return null;
        }

        return StitchSemanticTokenResponsesTogether(csharpResponse.Tokens);
    }

    // Internal for testing
    internal static int[] StitchSemanticTokenResponsesTogether(int[][]? responseData)
    {
        // Each inner array in `responseData` represents a single C# document that is broken down into a list of tokens.
        // This method stitches these lists of tokens together into a single, coherent list of semantic tokens.
        // The resulting array is a flattened version of the input array, and is in the precise format expected by the Microsoft Language Server Protocol.
        if (responseData is null || responseData.Length == 0)
        {
            return Array.Empty<int>();
        }

        if (responseData.Length == 1)
        {
            return responseData[0];
        }

        var count = responseData.Sum(r => r.Length);
        var data = new int[count];
        var dataIndex = 0;
        var lastTokenLine = 0;

        for (var i = 0; i < responseData.Length; i++)
        {
            var curData = responseData[i];

            if (curData.Length == 0)
            {
                continue;
            }

            Array.Copy(curData, 0, data, dataIndex, curData.Length);
            if (i != 0)
            {
                // The first two items in result.Data will potentially need it's line/col offset modified
                var lineDelta = data[dataIndex] - lastTokenLine;
                Debug.Assert(lineDelta >= 0);

                // Update the first line copied over from curData
                data[dataIndex] = lineDelta;

                // Update the first column copied over from curData if on the same line as the previous token
                if (lineDelta == 0)
                {
                    var lastTokenCol = 0;

                    // Walk back accumulating column deltas until we find a start column (indicated by it's line offset being non-zero)
                    for (var j = dataIndex - RazorSemanticTokensInfoService.TokenSize; j >= 0; j -= RazorSemanticTokensInfoService.TokenSize)
                    {
                        lastTokenCol += data[j + 1];
                        if (data[j] != 0)
                        {
                            break;
                        }
                    }

                    Debug.Assert(lastTokenCol >= 0);
                    data[dataIndex + 1] -= lastTokenCol;
                    Debug.Assert(data[dataIndex + 1] >= 0);
                }
            }

            lastTokenLine = 0;
            for (var j = 0; j < curData.Length; j += RazorSemanticTokensInfoService.TokenSize)
            {
                lastTokenLine += curData[j];
            }

            dataIndex += curData.Length;
        }

        return data;
    }

    // Internal for testing only
    internal static bool TryGetSortedCSharpRanges(RazorCodeDocument codeDocument, Range razorRange, [NotNullWhen(true)] out Range[]? ranges)
    {
        using var _ = ListPool<Range>.GetPooledObject(out var csharpRanges);
        var csharpSourceText = codeDocument.GetCSharpSourceText();
        var sourceText = codeDocument.GetSourceText();
        var textSpan = razorRange.AsTextSpan(sourceText);
        var csharpDoc = codeDocument.GetCSharpDocument();

        // We want to find the min and max C# source mapping that corresponds with our Razor range.
        foreach (var mapping in csharpDoc.SourceMappings)
        {
            var mappedTextSpan = mapping.OriginalSpan.AsTextSpan();

            if (textSpan.OverlapsWith(mappedTextSpan))
            {
                var mappedRange = mapping.GeneratedSpan.AsRange(csharpSourceText);
                csharpRanges.Add(mappedRange);
            }
        }

        if (csharpRanges.Count == 0)
        {
            ranges = null;
            return false;
        }

        ranges = csharpRanges.ToArray();
        // Ensure the C# ranges are sorted
        Array.Sort(ranges, static (r1, r2) => r1.CompareTo(r2));
        return true;
    }

    private static SemanticRange CSharpDataToSemanticRange(
        int lineDelta,
        int charDelta,
        int length,
        int tokenType,
        int tokenModifiers,
        SemanticRange? previousSemanticRange = null)
    {
        if (previousSemanticRange is null)
        {
            var previousRange = new Range
            {
                Start = new Position(0, 0),
                End = new Position(0, 0)
            };
            previousSemanticRange = new SemanticRange(0, previousRange, modifier: 0, fromRazor: false);
        }

        var startLine = previousSemanticRange.Range.End.Line + lineDelta;
        var previousEndChar = lineDelta == 0 ? previousSemanticRange.Range.Start.Character : 0;
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

    private static int[] ConvertSemanticRangesToSemanticTokensData(
        List<SemanticRange> semanticRanges,
        RazorCodeDocument razorCodeDocument)
    {
        SemanticRange? previousResult = null;

        var sourceText = razorCodeDocument.GetSourceText();

        // We don't bother filtering out duplicate ranges (eg, where C# and Razor both have opinions), but instead take advantage of
        // our sort algorithm to be correct, so we can skip duplicates here. That means our final array may end up smaller than the
        // expected size, so we have to use a list to build it.
        using var _ = ListPool<int>.GetPooledObject(out var data);
        data.SetCapacityIfLarger(semanticRanges.Count * TokenSize);

        foreach (var result in semanticRanges)
        {
            AppendData(result, previousResult, sourceText, data);

            previousResult = result;
        }

        return data.ToArray();

        // We purposely capture and manipulate the "data" array here to avoid allocation
        static void AppendData(
            SemanticRange currentRange,
            SemanticRange? previousRange,
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
            var previousLineIndex = previousRange?.Range is null ? 0 : previousRange.Range.Start.Line;
            var deltaLine = currentRange.Range.Start.Line - previousLineIndex;

            int deltaStart;
            if (previousRange != null && previousRange?.Range.Start.Line == currentRange.Range.Start.Line)
            {
                deltaStart = currentRange.Range.Start.Character - previousRange.Range.Start.Character;

                // If there is no line delta, no char delta, and this isn't the first range (ie, previousRange is not null)
                // then it means this range overlaps the previous, so we skip it.
                if (deltaStart == 0)
                {
                    return;
                }
            }
            else
            {
                deltaStart = currentRange.Range.Start.Character;
            }

            data.Add(deltaLine);
            data.Add(deltaStart);

            // length
            var textSpan = currentRange.Range.AsTextSpan(sourceText);
            var length = textSpan.Length;
            Debug.Assert(length > 0);
            data.Add(length);

            // tokenType
            data.Add(currentRange.Kind);

            // tokenModifiers
            data.Add(currentRange.Modifier);
        }
    }
}
