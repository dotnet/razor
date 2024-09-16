﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.Formatting;

/// <summary>
/// Gets edits in Razor files, and returns edits to Razor files, with nicely formatted Html
/// </summary>
internal sealed class CSharpFormattingPass(
    IDocumentMappingService documentMappingService,
    ILoggerFactory loggerFactory)
    : CSharpFormattingPassBase(documentMappingService, isFormatOnType: false)
{
    private readonly CSharpFormatter _csharpFormatter = new CSharpFormatter(documentMappingService);
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CSharpFormattingPass>();

    public async override Task<ImmutableArray<TextChange>> ExecuteAsync(FormattingContext context, ImmutableArray<TextChange> changes, CancellationToken cancellationToken)
    {
        // Apply previous edits if any.
        var originalText = context.SourceText;
        var changedText = originalText;
        var changedContext = context;
        if (changes.Length > 0)
        {
            changedText = changedText.WithChanges(changes);
            changedContext = await context.WithTextAsync(changedText).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Apply original C# edits
        var csharpChanges = await FormatCSharpAsync(changedContext, cancellationToken).ConfigureAwait(false);
        if (csharpChanges.Length > 0)
        {
            changedText = changedText.WithChanges(csharpChanges);
            changedContext = await changedContext.WithTextAsync(changedText).ConfigureAwait(false);

            _logger.LogTestOnly($"After FormatCSharpAsync:\r\n{changedText}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var indentationChanges = await AdjustIndentationAsync(changedContext, startLine: 0, endLineInclusive: changedText.Lines.Count - 1, cancellationToken).ConfigureAwait(false);
        if (indentationChanges.Length > 0)
        {
            // Apply the edits that modify indentation.
            changedText = changedText.WithChanges(indentationChanges);

            _logger.LogTestOnly($"After AdjustIndentationAsync:\r\n{changedText}");
        }

        _logger.LogTestOnly($"Generated C#:\r\n{context.CSharpSourceText}");

        return changedText.GetTextChangesArray(originalText);
    }

    private async Task<ImmutableArray<TextChange>> FormatCSharpAsync(FormattingContext context, CancellationToken cancellationToken)
    {
        var sourceText = context.SourceText;

        using var csharpChanges = new PooledArrayBuilder<TextChange>();
        foreach (var mapping in context.CodeDocument.GetCSharpDocument().SourceMappings)
        {
            var span = new TextSpan(mapping.OriginalSpan.AbsoluteIndex, mapping.OriginalSpan.Length);
            if (!ShouldFormat(context, span, allowImplicitStatements: true))
            {
                // We don't want to format this range.
                continue;
            }

            // These should already be remapped.
            var spanToFormat = sourceText.GetLinePositionSpan(span);
            var changes = await _csharpFormatter.FormatAsync(context, spanToFormat, cancellationToken).ConfigureAwait(false);
            csharpChanges.AddRange(changes.Where(e => spanToFormat.Contains(sourceText.GetLinePositionSpan(e.Span))));
        }

        return csharpChanges.ToImmutable();
    }
}
