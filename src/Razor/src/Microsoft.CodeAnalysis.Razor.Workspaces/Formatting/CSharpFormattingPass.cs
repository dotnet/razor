﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Formatting;

internal sealed class CSharpFormattingPass(
    IDocumentMappingService documentMappingService,
    ILoggerFactory loggerFactory)
    : CSharpFormattingPassBase(documentMappingService)
{
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CSharpFormattingPass>();

    // Run after the HTML and Razor formatter pass.
    public override int Order => DefaultOrder - 3;

    public async override Task<FormattingResult> ExecuteAsync(FormattingContext context, FormattingResult result, CancellationToken cancellationToken)
    {
        if (context.IsFormatOnType || result.Kind != RazorLanguageKind.Razor)
        {
            // We don't want to handle OnTypeFormatting here.
            return result;
        }

        // Apply previous edits if any.
        var originalText = context.SourceText;
        var changedText = originalText;
        var changedContext = context;
        if (result.Edits.Length > 0)
        {
            var changes = result.Edits.Select(originalText.GetTextChange).ToArray();
            changedText = changedText.WithChanges(changes);
            changedContext = await context.WithTextAsync(changedText).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Apply original C# edits
        var csharpEdits = await FormatCSharpAsync(changedContext, cancellationToken).ConfigureAwait(false);
        if (csharpEdits.Length > 0)
        {
            var csharpChanges = csharpEdits.Select(changedText.GetTextChange);
            changedText = changedText.WithChanges(csharpChanges);
            changedContext = await changedContext.WithTextAsync(changedText).ConfigureAwait(false);

            _logger.LogTestOnly($"After FormatCSharpAsync:\r\n{changedText}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var indentationChanges = await AdjustIndentationAsync(changedContext, cancellationToken).ConfigureAwait(false);
        if (indentationChanges.Count > 0)
        {
            // Apply the edits that modify indentation.
            changedText = changedText.WithChanges(indentationChanges);

            _logger.LogTestOnly($"After AdjustIndentationAsync:\r\n{changedText}");
        }

        _logger.LogTestOnly($"Generated C#:\r\n{context.CSharpSourceText}");

        var finalChanges = changedText.GetTextChanges(originalText);
        var finalEdits = finalChanges.Select(originalText.GetTextEdit).ToArray();

        return new FormattingResult(finalEdits);
    }

    private async Task<ImmutableArray<TextEdit>> FormatCSharpAsync(FormattingContext context, CancellationToken cancellationToken)
    {
        var sourceText = context.SourceText;

        using var csharpEdits = new PooledArrayBuilder<TextEdit>();
        foreach (var mapping in context.CodeDocument.GetCSharpDocument().SourceMappings)
        {
            var span = new TextSpan(mapping.OriginalSpan.AbsoluteIndex, mapping.OriginalSpan.Length);
            if (!ShouldFormat(context, span, allowImplicitStatements: true))
            {
                // We don't want to format this range.
                continue;
            }

            // These should already be remapped.
            var range = sourceText.GetRange(span);
            var edits = await CSharpFormatter.FormatAsync(context, range, cancellationToken).ConfigureAwait(false);
            csharpEdits.AddRange(edits.Where(e => range.Contains(e.Range)));
        }

        return csharpEdits.ToImmutable();
    }
}
