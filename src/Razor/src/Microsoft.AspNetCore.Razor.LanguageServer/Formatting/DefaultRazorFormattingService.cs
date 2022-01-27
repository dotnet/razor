// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

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
            DocumentUri uri,
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
            using var context = FormattingContext.Create(uri, documentSnapshot, codeDocument, options, _workspaceFactory, isFormatOnType: false, automaticallyAddUsings: false);

            var result = new FormattingResult(Array.Empty<TextEdit>());
            foreach (var pass in _formattingPasses)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result = await pass.ExecuteAsync(context, result, cancellationToken);
            }

            var filteredEdits = result.Edits.Where(e => range.LineOverlapsWith(e.Range)).ToArray();
            return filteredEdits;
        }

        public override Task<TextEdit[]> FormatOnTypeAsync(DocumentUri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, TextEdit[] formattedEdits, FormattingOptions options, CancellationToken cancellationToken)
            => ApplyFormattedEditsAsync(uri, documentSnapshot, kind, formattedEdits, options, bypassValidationPasses: false, collapseEdits: false, automaticallyAddUsings: false, cancellationToken: cancellationToken);

        public override Task<TextEdit[]> FormatCodeActionAsync(DocumentUri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, TextEdit[] formattedEdits, FormattingOptions options, CancellationToken cancellationToken)
            => ApplyFormattedEditsAsync(uri, documentSnapshot, kind, formattedEdits, options, bypassValidationPasses: true, collapseEdits: false, automaticallyAddUsings: true, cancellationToken: cancellationToken);

        public override async Task<TextEdit[]> FormatSnippetAsync(DocumentUri uri, DocumentSnapshot documentSnapshot, RazorLanguageKind kind, TextEdit[] formattedEdits, FormattingOptions options, CancellationToken cancellationToken)
        {
            if (kind == RazorLanguageKind.CSharp)
            {
                WrapCSharpSnippets(formattedEdits);
            }

            var edits = await ApplyFormattedEditsAsync(uri, documentSnapshot, kind, formattedEdits, options, bypassValidationPasses: true, collapseEdits: true, automaticallyAddUsings: false, cancellationToken: cancellationToken);

            if (kind == RazorLanguageKind.CSharp)
            {
                UnwrapCSharpSnippets(edits);
            }

            return edits;

            static void WrapCSharpSnippets(TextEdit[] snippetEdits)
            {
                for (var i = 0; i < snippetEdits.Length; i++)
                {
                    var snippetEdit = snippetEdits[i];

                    // Formatting doesn't work with syntax errors caused by the cursor marker ($0).
                    // So, let's avoid the error by wrapping the cursor marker in a comment.
                    var wrappedText = snippetEdit.NewText.Replace("$0", "/*$0*/");
                    snippetEdits[i] = snippetEdit with { NewText = wrappedText };
                }
            }

            static void UnwrapCSharpSnippets(TextEdit[] snippetEdits)
            {
                for (var i = 0; i < snippetEdits.Length; i++)
                {
                    var snippetEdit = snippetEdits[i];

                    // Unwrap the cursor marker.
                    var unwrappedText = snippetEdit.NewText.Replace("/*$0*/", "$0");
                    snippetEdits[i] = snippetEdit with { NewText = unwrappedText };
                }
            }
        }

        private async Task<TextEdit[]> ApplyFormattedEditsAsync(
            DocumentUri uri,
            DocumentSnapshot documentSnapshot,
            RazorLanguageKind kind,
            TextEdit[] formattedEdits,
            FormattingOptions options,
            bool bypassValidationPasses,
            bool collapseEdits,
            bool automaticallyAddUsings,
            CancellationToken cancellationToken)
        {
            if (kind == RazorLanguageKind.Html)
            {
                // We don't support formatting HTML edits yet.
                return formattedEdits;
            }

            // If we only received a single edit, let's always return a single edit back.
            // Otherwise, merge only if explicitly asked.
            collapseEdits |= formattedEdits.Length == 1;

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync();
            using var context = FormattingContext.Create(uri, documentSnapshot, codeDocument, options, _workspaceFactory, isFormatOnType: true, automaticallyAddUsings: automaticallyAddUsings);
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
