// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using TextSpan = Microsoft.CodeAnalysis.Text.TextSpan;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class CSharpFormattingPass : CSharpFormattingPassBase
    {
        private readonly ILogger _logger;

        public CSharpFormattingPass(
            RazorDocumentMappingService documentMappingService,
            FilePathNormalizer filePathNormalizer,
            ClientNotifierServiceBase server,
            ILoggerFactory loggerFactory)
            : base(documentMappingService, filePathNormalizer, server)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<CSharpFormattingPass>();
        }

        // Run after the HTML formatter pass.
        public override int Order => DefaultOrder - 4;

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
                var changes = result.Edits.Select(e => e.AsTextChange(originalText)).ToArray();
                changedText = changedText.WithChanges(changes);
                changedContext = await context.WithTextAsync(changedText);
            }

            _logger.LogInformation("Before C# Formatting:\r\n" + originalText);
            _logger.LogInformation("After Previous Edits:\r\n" + changedText);

            cancellationToken.ThrowIfCancellationRequested();

            // Apply original C# edits
            var csharpEdits = await FormatCSharpAsync(changedContext, cancellationToken);
            if (csharpEdits.Count > 0)
            {
                var csharpChanges = csharpEdits.Select(c => c.AsTextChange(changedText));
                changedText = changedText.WithChanges(csharpChanges);
                changedContext = await changedContext.WithTextAsync(changedText);
            }

            _logger.LogInformation("After C# Formatting:\r\n" + changedText);

            cancellationToken.ThrowIfCancellationRequested();

            // We make an optimistic attempt at fixing corner cases.
            var cleanupChanges = CleanupDocument(changedContext);
            changedText = changedText.WithChanges(cleanupChanges);
            changedContext = await changedContext.WithTextAsync(changedText);

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("After Clean up:\r\n" + changedText);

            var indentationChanges = await AdjustIndentationAsync(changedContext, cancellationToken);
            if (indentationChanges.Count > 0)
            {
                // Apply the edits that modify indentation.
                changedText = changedText.WithChanges(indentationChanges);

                _logger.LogInformation("After Indentation:\r\n" + changedText);
            }

            var finalChanges = SourceTextDiffer.GetMinimalTextChanges(originalText, changedText, lineDiffOnly: false);
            var finalEdits = finalChanges.Select(f => f.AsTextEdit(originalText)).ToArray();

            return new FormattingResult(finalEdits);
        }

        private async Task<List<TextEdit>> FormatCSharpAsync(FormattingContext context, CancellationToken cancellationToken)
        {
            var sourceText = context.SourceText;
            var csharpEdits = new List<TextEdit>();

            _logger.LogInformation("CSharp Code:\r\n" + context.CodeDocument.GetCSharpDocument().GeneratedCode);
            _logger.LogInformation("Source Mappings:\r\n" + string.Join(Environment.NewLine, context.CodeDocument.GetCSharpDocument().SourceMappings));

            foreach (var mapping in context.CodeDocument.GetCSharpDocument().SourceMappings)
            {
                var span = new TextSpan(mapping.OriginalSpan.AbsoluteIndex, mapping.OriginalSpan.Length);
                if (!ShouldFormat(context, span, allowImplicitStatements: true))
                {
                    // We don't want to format this range.
                    continue;
                }

                // These should already be remapped.
                var range = span.AsRange(sourceText);
                var edits = await CSharpFormatter.FormatAsync(context, range, cancellationToken);

                _logger.LogInformation("Formatting:\r\n" + sourceText.GetSubTextString(span));
                var collection = edits.Where(e => range.Contains(e.Range));
                _logger.LogInformation("Edits:\r\n" + string.Join(Environment.NewLine, collection));
                //_logger.LogInformation("After:\r\n" + sourceText.WithChanges(collection.Select(t => t.AsTextChange(sourceText))).GetSubTextString(span));

                csharpEdits.AddRange(collection);
            }

            return csharpEdits;
        }
    }
}
