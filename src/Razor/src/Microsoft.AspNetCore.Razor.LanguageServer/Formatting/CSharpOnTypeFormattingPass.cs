// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class CSharpOnTypeFormattingPass : FormattingPassBase
    {
        private readonly ILogger _logger;

        public CSharpOnTypeFormattingPass(
            RazorDocumentMappingService documentMappingService,
            FilePathNormalizer filePathNormalizer,
            IClientLanguageServer server,
            ILoggerFactory loggerFactory)
            : base(documentMappingService, filePathNormalizer, server)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<CSharpOnTypeFormattingPass>();
        }

        public override bool IsValidationPass => false;

        public async override Task<FormattingResult> ExecuteAsync(FormattingContext context, FormattingResult result, CancellationToken cancellationToken)
        {
            if (!context.IsFormatOnType || result.Kind != RazorLanguageKind.CSharp)
            {
                // We don't want to handle regular formatting or non-C# on type formatting here.
                return result;
            }

            // Normalize and re-map the C# edits.
            var codeDocument = context.CodeDocument;
            var csharpText = SourceText.From(codeDocument.GetCSharpDocument().GeneratedCode);
            var normalizedEdits = NormalizeTextEdits(csharpText, result.Edits);
            var mappedEdits = RemapTextEdits(codeDocument, normalizedEdits, result.Kind);
            var filteredEdits = FilterCSharpTextEdits(context, mappedEdits);
            if (filteredEdits.Length == 0)
            {
                // There are no CSharp edits for us to apply. No op.
                return new FormattingResult(filteredEdits);
            }

            // Find the lines that were affected by these edits.
            var originalText = codeDocument.GetSourceText();
            var changes = filteredEdits.Select(e => e.AsTextChange(originalText));

            // Apply the format on type edits sent over by the client.
            var formattedText = originalText.WithChanges(changes);
            TrackEncompassingChange(originalText, formattedText, out _, out var spanAfterChange);
            var rangeAfterEdit = spanAfterChange.AsRange(formattedText);
            var changedContext = await context.WithTextAsync(formattedText);

            cancellationToken.ThrowIfCancellationRequested();

            // We make an optimistic attempt at fixing corner cases.
            var cleanedText = CleanupDocument(changedContext, rangeAfterEdit);
            TrackEncompassingChange(formattedText, cleanedText, out _, out spanAfterChange);
            rangeAfterEdit = spanAfterChange.AsRange(cleanedText);
            changedContext = await changedContext.WithTextAsync(cleanedText);

            cancellationToken.ThrowIfCancellationRequested();

            // At this point we should have applied all edits that adds/removes newlines.
            // Let's now ensure the indentation of each of those lines is correct.
            var indentationChanges = AdjustIndentation(changedContext, cancellationToken, rangeAfterEdit);
            if (indentationChanges.Count > 0)
            {
                // Apply the edits that modify indentation.
                cleanedText = cleanedText.WithChanges(indentationChanges);
            }

            // Now that we have made all the necessary changes to the document. Let's diff the original vs final version and return the diff.
            var finalChanges = SourceTextDiffer.GetMinimalTextChanges(originalText, cleanedText, lineDiffOnly: false);
            var finalEdits = finalChanges.Select(f => f.AsTextEdit(originalText)).ToArray();

            return new FormattingResult(finalEdits);
        }

        private TextEdit[] FilterCSharpTextEdits(FormattingContext context, TextEdit[] edits)
        {
            var filteredEdits = edits.Where(e => ShouldFormat(context, e.Range.Start, allowImplicitStatements: false)).ToArray();
            return filteredEdits;
        }
    }
}
