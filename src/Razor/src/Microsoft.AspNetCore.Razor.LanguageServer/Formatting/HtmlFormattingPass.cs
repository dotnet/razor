﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using TextSpan = Microsoft.CodeAnalysis.Text.TextSpan;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class HtmlFormattingPass : FormattingPassBase
    {
        private readonly ILogger _logger;

        public HtmlFormattingPass(
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

            _logger = loggerFactory.CreateLogger<HtmlFormattingPass>();

            HtmlFormatter = new HtmlFormatter(server, filePathNormalizer);
        }

        // We want this to run first because it uses the client HTML formatter.
        public override int Order => DefaultOrder - 5;

        public override bool IsValidationPass => false;

        protected HtmlFormatter HtmlFormatter { get; }

        public async override Task<FormattingResult> ExecuteAsync(FormattingContext context, FormattingResult result, CancellationToken cancellationToken)
        {
            if (context.IsFormatOnType)
            {
                // We don't want to handle OnTypeFormatting here.
                return result;
            }

            var originalText = context.SourceText;

            var htmlEdits = await HtmlFormatter.FormatAsync(context, cancellationToken);

            // Allow benchmarks to specify a different diff algorithm
            if (!context.Options.TryGetValue("UseSourceTextDiffer", out var useSourceTextDiffer))
            {
                useSourceTextDiffer = new BooleanNumberString(false);
            }

            var normalizedEdits = htmlEdits;
            if (useSourceTextDiffer.Bool)
            {
                normalizedEdits = NormalizeTextEdits(originalText, htmlEdits);
            }

            var changedText = originalText;
            var changedContext = context;

            if (normalizedEdits.Length > 0)
            {
                var changes = normalizedEdits.Select(e => e.AsTextChange(originalText));
                changedText = originalText.WithChanges(changes);
                // Create a new formatting context for the changed razor document.
                changedContext = await context.WithTextAsync(changedText);
            }

            var indentationChanges = AdjustRazorIndentation(changedContext);
            if (indentationChanges.Count > 0)
            {
                // Apply the edits that adjust indentation.
                changedText = changedText.WithChanges(indentationChanges);
            }

            var finalChanges = useSourceTextDiffer.Bool ? SourceTextDiffer.GetMinimalTextChanges(originalText, changedText, lineDiffOnly: false) : changedText.GetTextChanges(originalText);
            var finalEdits = finalChanges.Select(f => f.AsTextEdit(originalText)).ToArray();

            return new FormattingResult(finalEdits);
        }

        private static TextEdit[] NormalizeTextEdits(SourceText originalText, TextEdit[] edits)
        {
            var changes = edits.Select(e => e.AsTextChange(originalText));
            var changedText = originalText.WithChanges(changes);
            var cleanChanges = SourceTextDiffer.GetMinimalTextChanges(originalText, changedText, lineDiffOnly: false);
            var cleanEdits = cleanChanges.Select(c => c.AsTextEdit(originalText)).ToArray();
            return cleanEdits;
        }

        private static List<TextChange> AdjustRazorIndentation(FormattingContext context)
        {
            // Assume HTML formatter has already run at this point and HTML is relatively indented correctly.
            // But HTML doesn't know about Razor blocks.
            // Our goal here is to indent each line according to the surrounding Razor blocks.
            var sourceText = context.SourceText;
            var editsToApply = new List<TextChange>();
            var indentations = context.GetIndentations();

            for (var i = 0; i < sourceText.Lines.Count; i++)
            {
                var line = sourceText.Lines[i];
                if (line.Span.Length == 0)
                {
                    // Empty line.
                    continue;
                }

                if (indentations[i].StartsInCSharpContext)
                {
                    // Normally we don't do HTML things in C# contexts but there is one
                    // edge case when including render fragments in a C# code block, eg:
                    //
                    // @code {
                    //      void Foo()
                    //      {
                    //          Render(@<SurveyPrompt />);
                    //      {
                    // }
                    //
                    // This is popular in some libraries, like bUnit. The issue here is that
                    // the HTML formatter sees ~~~~~<SurveyPrompt /> and puts a newline before
                    // the tag, but obviously that breaks things.
                    //
                    // It's straight forward enough to just check for this situation and special case
                    // it by removing the newline again.

                    // There needs to be at least one more line, and the current line needs to end with
                    // an @ sign, and have an open angle bracket at the start of the next line.
                    if (sourceText.Lines.Count >= i + 1 &&
                        line.Text?.Length > 1 &&
                        line.Text?[line.End - 1] == '@')
                    {
                        var nextLine = sourceText.Lines[i + 1];
                        var firstChar = nextLine.GetFirstNonWhitespaceOffset().GetValueOrDefault();

                        // When the HTML formatter inserts the newline in this scenario, it doesn't
                        // indent the component tag, so we use that as another signal that this is
                        // the scenario we think it is.
                        if (firstChar == 0 &&
                            nextLine.Text?[nextLine.Start] == '<')
                        {
                            var lineBreakLength = line.EndIncludingLineBreak - line.End;
                            var spanToReplace = new TextSpan(line.End, lineBreakLength);
                            var change = new TextChange(spanToReplace, string.Empty);
                            editsToApply.Add(change);

                            // Skip the next line because we've essentially just removed it.
                            i++;
                        }
                    }

                    continue;
                }

                var razorDesiredIndentationLevel = indentations[i].RazorIndentationLevel;
                if (razorDesiredIndentationLevel == 0)
                {
                    // This line isn't under any Razor specific constructs. Trust the HTML formatter.
                    continue;
                }

                var htmlDesiredIndentationLevel = indentations[i].HtmlIndentationLevel;
                if (htmlDesiredIndentationLevel == 0 && !IsPartOfHtmlTag(context, indentations[i].FirstSpan.Span.Start))
                {
                    // This line is under some Razor specific constructs but not under any HTML tag.
                    // E.g,
                    // @{
                    //          @* comment *@ <----
                    // }
                    //
                    // In this case, the HTML formatter wouldn't touch it but we should format it correctly.
                    // So, let's use our syntax understanding to rewrite the indentation.
                    // Note: This case doesn't apply for HTML tags (HTML formatter will touch it even if it is in the root).
                    // Hence the second part of the if condition.
                    //
                    var desiredIndentationLevel = indentations[i].IndentationLevel;
                    var desiredIndentationString = context.GetIndentationLevelString(desiredIndentationLevel);
                    var spanToReplace = new TextSpan(line.Start, indentations[i].ExistingIndentation);
                    var change = new TextChange(spanToReplace, desiredIndentationString);
                    editsToApply.Add(change);
                }
                else
                {
                    // This line is under some Razor specific constructs and HTML tags.
                    // E.g,
                    // @{
                    //    <div class="foo"
                    //         id="oof">  <----
                    //    </div>
                    // }
                    //
                    // In this case, the HTML formatter would've formatted it correctly. Let's not use our syntax understanding.
                    // Instead, we should just add to the existing indentation.
                    //
                    var razorDesiredIndentationString = context.GetIndentationLevelString(razorDesiredIndentationLevel);
                    var existingIndentationString = context.GetIndentationString(indentations[i].ExistingIndentationSize);
                    var desiredIndentationString = existingIndentationString + razorDesiredIndentationString;
                    var spanToReplace = new TextSpan(line.Start, indentations[i].ExistingIndentation);
                    var change = new TextChange(spanToReplace, desiredIndentationString);
                    editsToApply.Add(change);
                }
            }

            return editsToApply;
        }

        private static bool IsPartOfHtmlTag(FormattingContext context, int position)
        {
            var syntaxTree = context.CodeDocument.GetSyntaxTree();
            var change = new SourceChange(position, 0, string.Empty);
            var owner = syntaxTree.Root.LocateOwner(change);
            if (owner is null)
            {
                // Can't determine owner of this position.
                return false;
            }

            // E.g, (| is position)
            //
            // `<p csharpattr="|Variable">` - true
            //
            return owner.AncestorsAndSelf().Any(
                n => n is MarkupStartTagSyntax || n is MarkupTagHelperStartTagSyntax || n is MarkupEndTagSyntax || n is MarkupTagHelperEndTagSyntax);
        }
    }
}
