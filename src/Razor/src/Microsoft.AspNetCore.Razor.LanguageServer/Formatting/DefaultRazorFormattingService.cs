// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class DefaultRazorFormattingService : RazorFormattingService
    {
        private readonly List<IFormattingPass> _formattingPasses;
        private readonly ILogger _logger;
        private readonly AdhocWorkspaceFactory _workspaceFactory;

        public DefaultRazorFormattingService(
            IEnumerable<IFormattingPass> formattingPasses,
            ILoggerFactory loggerFactory,
            AdhocWorkspaceFactory workspaceFactory)
        {
            if (formattingPasses is null)
            {
                throw new ArgumentNullException(nameof(formattingPasses));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (workspaceFactory is null)
            {
                throw new ArgumentNullException(nameof(workspaceFactory));
            }

            _formattingPasses = formattingPasses.OrderBy(f => f.Order).ToList();
            _logger = loggerFactory.CreateLogger<DefaultRazorFormattingService>();
            _workspaceFactory = workspaceFactory;
        }

        public override async Task<TextEdit[]> FormatAsync(
            Uri uri,
            DocumentSnapshot documentSnapshot,
            Range range,
            FormattingOptions options,
            CancellationToken cancellationToken)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (documentSnapshot is null)
            {
                throw new ArgumentNullException(nameof(documentSnapshot));
            }

            if (range is null)
            {
                throw new ArgumentNullException(nameof(range));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync();
            using var context = FormattingContext.Create(uri, documentSnapshot, codeDocument, options, _workspaceFactory);
            var originalText = context.SourceText;

            var result = new FormattingResult(Array.Empty<TextEdit>());
            foreach (var pass in _formattingPasses)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = await pass.ExecuteAsync(context, result, cancellationToken);
            }

            var filteredEdits = result.Edits.Where(e => range.LineOverlapsWith(e.Range));

            // Make sure the edits actually change something, or its not worth responding
            var textChanges = filteredEdits.Select(e => e.AsTextChange(originalText));
            var changedText = originalText.WithChanges(textChanges);
            if (changedText.ContentEquals(originalText))
            {
                return Array.Empty<TextEdit>();
            }

            // Only send back the minimum edits
            var minimalChanges = SourceTextDiffer.GetMinimalTextChanges(originalText, changedText, lineDiffOnly: false);
            var finalEdits = minimalChanges.Select(f => f.AsTextEdit(originalText)).ToArray();

            return finalEdits;
        }

        public override Task<TextEdit[]> FormatOnTypeAsync(Uri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, TextEdit[] formattedEdits, FormattingOptions options, int hostDocumentIndex, char triggerCharacter, CancellationToken cancellationToken)
            => ApplyFormattedEditsAsync(uri, documentSnapshot, kind, formattedEdits, options, hostDocumentIndex, triggerCharacter, bypassValidationPasses: false, collapseEdits: false, automaticallyAddUsings: false, cancellationToken: cancellationToken);

        public override Task<TextEdit[]> FormatCodeActionAsync(Uri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, TextEdit[] formattedEdits, FormattingOptions options, CancellationToken cancellationToken)
            => ApplyFormattedEditsAsync(uri, documentSnapshot, kind, formattedEdits, options, hostDocumentIndex: 0, triggerCharacter: '\0', bypassValidationPasses: true, collapseEdits: false, automaticallyAddUsings: true, cancellationToken: cancellationToken);

        public override Task<TextEdit[]> FormatSnippetAsync(Uri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, TextEdit[] formattedEdits, FormattingOptions options, CancellationToken cancellationToken)
            => ApplyFormattedEditsAsync(uri, documentSnapshot, kind, formattedEdits, options, hostDocumentIndex: 0, triggerCharacter: '\0', bypassValidationPasses: true, collapseEdits: true, automaticallyAddUsings: false, cancellationToken: cancellationToken);

        private async Task<TextEdit[]> ApplyFormattedEditsAsync(
            Uri uri,
            DocumentSnapshot documentSnapshot,
            RazorLanguageKind kind,
            TextEdit[] formattedEdits,
            FormattingOptions options,
            int hostDocumentIndex,
            char triggerCharacter,
            bool bypassValidationPasses,
            bool collapseEdits,
            bool automaticallyAddUsings,
            CancellationToken cancellationToken)
        {
            // If we only received a single edit, let's always return a single edit back.
            // Otherwise, merge only if explicitly asked.
            collapseEdits |= formattedEdits.Length == 1;

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync();
            using var context = FormattingContext.CreateForOnTypeFormatting(uri, documentSnapshot, codeDocument, options, _workspaceFactory, automaticallyAddUsings: automaticallyAddUsings, hostDocumentIndex, triggerCharacter);
            var result = new FormattingResult(formattedEdits, kind);

            foreach (var pass in _formattingPasses)
            {
                if (pass.IsValidationPass && bypassValidationPasses)
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();
                result = await pass.ExecuteAsync(context, result, cancellationToken);
            }

            var originalText = context.SourceText;
            var edits = result.Edits;
            if (collapseEdits)
            {
                var collapsedEdit = MergeEdits(result.Edits, originalText);
                edits = new[] { collapsedEdit };
            }

            // Make sure the edits actually change something, or its not worth responding
            var textChanges = edits.Select(e => e.AsTextChange(originalText));
            var changedText = originalText.WithChanges(textChanges);

            if (changedText.ContentEquals(originalText))
            {
                return Array.Empty<TextEdit>();
            }

            return edits;
        }

        // Internal for testing
        internal static TextEdit MergeEdits(TextEdit[] edits, SourceText sourceText)
        {
            if (edits.Length == 1)
            {
                return edits[0];
            }

            var textChanges = new List<TextChange>();
            foreach (var edit in edits)
            {
                var change = new TextChange(edit.Range.AsTextSpan(sourceText), edit.NewText);
                textChanges.Add(change);
            }

            var changedText = sourceText.WithChanges(textChanges);
            var affectedRange = changedText.GetEncompassingTextChangeRange(sourceText);
            var spanBeforeChange = affectedRange.Span;
            var spanAfterChange = new TextSpan(spanBeforeChange.Start, affectedRange.NewLength);
            var newText = changedText.GetSubTextString(spanAfterChange);

            var encompassingChange = new TextChange(spanBeforeChange, newText);

            return encompassingChange.AsTextEdit(sourceText);
        }
    }
}
