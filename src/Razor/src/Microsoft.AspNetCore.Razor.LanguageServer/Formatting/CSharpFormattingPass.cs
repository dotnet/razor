﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using TextSpan = Microsoft.CodeAnalysis.Text.TextSpan;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

internal class CSharpFormattingPass : CSharpFormattingPassBase
{
    private readonly ILogger _logger;

    public CSharpFormattingPass(
        IRazorDocumentMappingService documentMappingService,
        ClientNotifierServiceBase server,
        ILoggerFactory loggerFactory)
        : base(documentMappingService, server)
    {
        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _logger = loggerFactory.CreateLogger<CSharpFormattingPass>();
    }

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
            var changes = result.Edits.Select(e => e.ToTextChange(originalText)).ToArray();
            changedText = changedText.WithChanges(changes);
            changedContext = await context.WithTextAsync(changedText).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Apply original C# edits
        var csharpEdits = await FormatCSharpAsync(changedContext, cancellationToken).ConfigureAwait(false);
        if (csharpEdits.Count > 0)
        {
            var csharpChanges = csharpEdits.Select(c => c.ToTextChange(changedText));
            changedText = changedText.WithChanges(csharpChanges);
            changedContext = await changedContext.WithTextAsync(changedText).ConfigureAwait(false);

            _logger.LogTestOnly("After FormatCSharpAsync:\r\n{changedText}", changedText);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var indentationChanges = await AdjustIndentationAsync(changedContext, cancellationToken).ConfigureAwait(false);
        if (indentationChanges.Count > 0)
        {
            // Apply the edits that modify indentation.
            changedText = changedText.WithChanges(indentationChanges);

            _logger.LogTestOnly("After AdjustIndentationAsync:\r\n{changedText}", changedText);
        }

        _logger.LogTestOnly("Generated C#:\r\n{context.CSharpSourceText}", context.CSharpSourceText);

        var finalChanges = changedText.GetTextChanges(originalText);
        var finalEdits = finalChanges.Select(f => f.ToTextEdit(originalText)).ToArray();

        return new FormattingResult(finalEdits);
    }

    private async Task<List<TextEdit>> FormatCSharpAsync(FormattingContext context, CancellationToken cancellationToken)
    {
        var sourceText = context.SourceText;
        var csharpEdits = new List<TextEdit>();
        foreach (var mapping in context.CodeDocument.GetCSharpDocument().SourceMappings)
        {
            var span = new TextSpan(mapping.OriginalSpan.AbsoluteIndex, mapping.OriginalSpan.Length);
            if (!ShouldFormat(context, span, allowImplicitStatements: true))
            {
                // We don't want to format this range.
                continue;
            }

            // These should already be remapped.
            var range = span.ToRange(sourceText);
            var edits = await CSharpFormatter.FormatAsync(context, range, cancellationToken).ConfigureAwait(false);
            csharpEdits.AddRange(edits.Where(e => range.Contains(e.Range)));
        }

        return csharpEdits;
    }
}
