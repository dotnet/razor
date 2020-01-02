// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using TextSpan = Microsoft.CodeAnalysis.Text.TextSpan;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class DefaultRazorFormattingService : RazorFormattingService
    {
        private readonly ILanguageServer _server;
        private readonly CSharpFormatter _cSharpFormatter;
        private readonly HtmlFormatter _htmlFormatter;
        private readonly ILogger _logger;

        public DefaultRazorFormattingService(
            RazorDocumentMappingService documentMappingService,
            FilePathNormalizer filePathNormalizer,
            ILanguageServer server,
            ILoggerFactory loggerFactory)
        {
            if (documentMappingService is null)
            {
                throw new ArgumentNullException(nameof(documentMappingService));
            }

            if (filePathNormalizer is null)
            {
                throw new ArgumentNullException(nameof(filePathNormalizer));
            }

            if (server is null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _server = server;
            _cSharpFormatter = new CSharpFormatter(documentMappingService, server, filePathNormalizer);
            _htmlFormatter = new HtmlFormatter(server, filePathNormalizer);
            _logger = loggerFactory.CreateLogger<DefaultRazorFormattingService>();
        }

        public override async Task<TextEdit[]> FormatAsync(Uri uri, RazorCodeDocument codeDocument, Range range, FormattingOptions options)
        {
            if (uri is null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            if (range is null)
            {
                throw new ArgumentNullException(nameof(range));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var formattingContext = CreateFormattingContext(uri, codeDocument, range, options);
            var edits = await FormatCodeBlockDirectivesAsync(formattingContext);
            return edits;
        }

        private async Task<TextEdit[]> FormatCodeBlockDirectivesAsync(FormattingContext context)
        {
            var source = context.CodeDocument.Source;
            var syntaxTree = context.CodeDocument.GetSyntaxTree();
            var nodes = syntaxTree.GetCodeBlockDirectives();

            var allEdits = new List<TextEdit>();

            // Iterate in reverse so that the newline changes don't affect the next code block directive.
            for (var i = nodes.Length - 1; i >= 0; i--)
            {
                var directive = nodes[i];
                if (!(directive.Body is RazorDirectiveBodySyntax directiveBody))
                {
                    // This can't happen realistically. Just being defensive.
                    continue;
                }

                // Get the inner code block node that contains the actual code.
                var innerCodeBlockNode = directiveBody.CSharpCode.DescendantNodes().FirstOrDefault(n => n is CSharpCodeBlockSyntax);
                if (innerCodeBlockNode == null)
                {
                    // Nothing to indent.
                    continue;
                }

                if (innerCodeBlockNode.DescendantNodes().Any(n =>
                    n is MarkupBlockSyntax ||
                    n is CSharpTransitionSyntax ||
                    n is RazorCommentBlockSyntax))
                {
                    // We currently don't support formatting code block directives with Markup or other Razor constructs.
                    continue;
                }

                var originalText = context.SourceText;
                var changedText = originalText;
                var codeBlockRange = innerCodeBlockNode.GetRange(source);
                var rangeToFormat = codeBlockRange.Overlap(context.Range);
                if (rangeToFormat != null)
                {
                    var codeEdits = await _cSharpFormatter.FormatAsync(context.CodeDocument, rangeToFormat, context.Uri, context.Options);
                    changedText = ApplyCSharpEdits(context, codeBlockRange, codeEdits, minCSharpIndentLevel: 2);
                }

                var edits = new List<TextEdit>();
                FormatCodeBlockStart(context, changedText, directiveBody, innerCodeBlockNode, edits);
                FormatCodeBlockEnd(context, changedText, directiveBody, innerCodeBlockNode, edits);
                changedText = ApplyChanges(changedText, edits.Select(e => e.AsTextChange(changedText)));

                var fullCodeBlockDirectiveSpan = GetSpanIncludingPrecedingWhitespaceInLine(originalText, directive.Position, directive.EndPosition);
                var changes = Diff(originalText, changedText, fullCodeBlockDirectiveSpan);

                var transformedEdits = changes.Select(c => c.AsTextEdit(originalText));
                allEdits.AddRange(transformedEdits);
            }

            return allEdits.ToArray();
        }

        // minCSharpIndentLevel refers to the minimum level of how much the C# formatter would indent code.
        // This is typically 2 for @code/@functions blocks and 3 for @{} blocks and other Razor blocks.
        private SourceText ApplyCSharpEdits(FormattingContext context, Range codeBlockRange, TextEdit[] edits, int minCSharpIndentLevel)
        {
            var originalText = context.SourceText;
            var originalCodeBlockSpan = codeBlockRange.AsTextSpan(originalText);

            // Sometimes the C# formatter edits outside the range we supply. Filter out those edits.
            var changes = edits.Select(e => e.AsTextChange(originalText)).Where(c => originalCodeBlockSpan.Contains(c.Span)).ToArray();
            if (changes.Length == 0)
            {
                return originalText;
            }

            // Apply the C# edits to the document.
            var changedText = originalText.WithChanges(changes);
            TrackChangeInSpan(originalText, originalCodeBlockSpan, changedText, out var changedCodeBlockSpan, out var changeEncompassingSpan);

            // We now have the changed document with C# edits. But it might be indented more/less than what we want depending on the context.
            // So, we want to bring each line to the right level of indentation based on where the block is in the document.
            // We also need to only do this for the lines that are part of the input range to respect range formatting.
            var desiredIndentationLevel = context.Indentations[(int)codeBlockRange.Start.Line].IndentationLevel + 1;
            var editsToApply = new List<TextChange>();
            var inputSpan = context.Range.AsTextSpan(originalText);
            TrackChangeInSpan(originalText, inputSpan, changedText, out var changedInputSpan, out _);
            var changedInputRange = changedInputSpan.AsRange(changedText);

            for (var i = (int)changedInputRange.Start.Line; i <= changedInputRange.End.Line; i++)
            {
                var line = changedText.Lines[i];
                if (line.Span.Length == 0)
                {
                    // Empty line. C# formatter didn't remove it so we won't either.
                    continue;
                }

                if (!changedCodeBlockSpan.Contains(line.Start))
                {
                    // Defensive check to make sure we're not handling lines that are not part of the current code block.
                    continue;
                }

                var leadingWhitespace = line.GetLeadingWhitespace();
                var minCSharpIndentLength = context.Options.TabSize * minCSharpIndentLevel;
                if (leadingWhitespace.Length < minCSharpIndentLength)
                {
                    // For whatever reason, the C# formatter decided to not indent this. Leave it as is.
                    continue;
                }
                else
                {
                    var effectiveDesiredIndentationLevel = desiredIndentationLevel - minCSharpIndentLevel;
                    if (effectiveDesiredIndentationLevel < 0)
                    {
                        // This means that we need to unindent.
                        var length = Math.Abs(effectiveDesiredIndentationLevel * context.Options.TabSize);
                        var span = new TextSpan(line.Start, (int)length);
                        editsToApply.Add(new TextChange(span, string.Empty));
                    }
                    else if (effectiveDesiredIndentationLevel > 0)
                    {
                        // This means that we need to indent.
                        var effectiveDesiredIndentation = GetIndentationString(context, effectiveDesiredIndentationLevel);
                        var span = new TextSpan(line.Start, 0);
                        editsToApply.Add(new TextChange(span, effectiveDesiredIndentation));
                    }
                }
            }

            changedText = ApplyChanges(changedText, editsToApply);
            return changedText;
        }

        private void FormatCodeBlockStart(FormattingContext context, SourceText changedText, RazorDirectiveBodySyntax directiveBody, SyntaxNode innerCodeBlock, List<TextEdit> edits)
        {
            var sourceText = context.SourceText;
            var originalBodySpan = TextSpan.FromBounds(directiveBody.Position, directiveBody.EndPosition);
            var originalBodyRange = originalBodySpan.AsRange(sourceText);
            if (context.Range.Start.Line > originalBodyRange.Start.Line)
            {
                return;
            }

            // First line is within the selected range. Let's try and format the start.

            TrackChangeInSpan(sourceText, originalBodySpan, changedText, out var changedBodySpan, out _);
            var changedBodyRange = changedBodySpan.AsRange(changedText);

            // First, make sure the first line is indented correctly.
            var firstLine = changedText.Lines[(int)changedBodyRange.Start.Line];
            var desiredIndentationLevel = context.Indentations[firstLine.LineNumber].IndentationLevel;
            var desiredIndentation = GetIndentationString(context, desiredIndentationLevel);
            var firstNonWhitespaceOffset = firstLine.GetFirstNonWhitespaceOffset();
            if (firstNonWhitespaceOffset.HasValue)
            {
                var edit = new TextEdit()
                {
                    Range = new Range(
                        new Position(firstLine.LineNumber, 0),
                        new Position(firstLine.LineNumber, firstNonWhitespaceOffset.Value)),
                    NewText = desiredIndentation
                };
                edits.Add(edit);
            }

            // We should also move any code that comes after '{' down to its own line.
            var originalInnerCodeBlockSpan = TextSpan.FromBounds(innerCodeBlock.Position, innerCodeBlock.EndPosition);
            TrackChangeInSpan(sourceText, originalInnerCodeBlockSpan, changedText, out var changedInnerCodeBlockSpan, out _);
            var innerCodeBlockRange = changedInnerCodeBlockSpan.AsRange(changedText);

            var innerCodeBlockLine = changedText.Lines[(int)innerCodeBlockRange.Start.Line];
            var textAfterBlockStart = innerCodeBlockLine.ToString().Substring(innerCodeBlock.Position - innerCodeBlockLine.Start);
            var isBlockStartOnSeparateLine = string.IsNullOrWhiteSpace(textAfterBlockStart);
            var innerCodeBlockIndentationLevel = desiredIndentationLevel + 1;
            var desiredInnerCodeBlockIndentation = GetIndentationString(context, innerCodeBlockIndentationLevel);
            var whitespaceAfterBlockStart = textAfterBlockStart.GetLeadingWhitespace();

            if (!isBlockStartOnSeparateLine)
            {
                // If the first line contains code, add a newline at the beginning and indent it.
                var edit = new TextEdit()
                {
                    Range = new Range(
                        new Position(innerCodeBlockLine.LineNumber, innerCodeBlock.Position - innerCodeBlockLine.Start),
                        new Position(innerCodeBlockLine.LineNumber, innerCodeBlock.Position + whitespaceAfterBlockStart.Length - innerCodeBlockLine.Start)),
                    NewText = Environment.NewLine + desiredInnerCodeBlockIndentation
                };
                edits.Add(edit);
            }
            else
            {
                // The code inside the code block directive is on its own line. Ideally the C# formatter would have already taken care of it.
                // Except, the first line of the code block is not indented because of how our SourceMappings work.
                // The edit for the first line is filtered out due to lack of a matching source mapping.
                // So we should manually indent that line here.

                var innerCodeBlockText = changedText.GetSubTextString(changedInnerCodeBlockSpan);
                if (!string.IsNullOrWhiteSpace(innerCodeBlockText))
                {
                    var codeStart = innerCodeBlockText.GetFirstNonWhitespaceOffset() + changedInnerCodeBlockSpan.Start;
                    if (codeStart.HasValue && codeStart != changedInnerCodeBlockSpan.End)
                    {
                        // If we got here, it means this is a non-empty code block. We can safely indent the first line.
                        var codeStartLine = changedText.Lines.GetLineFromPosition(codeStart.Value);
                        var existingCodeStartIndentation = codeStartLine.GetFirstNonWhitespaceOffset() ?? 0;
                        var edit = new TextEdit()
                        {
                            Range = new Range(
                                new Position(codeStartLine.LineNumber, 0),
                                new Position(codeStartLine.LineNumber, existingCodeStartIndentation)),
                            NewText = desiredInnerCodeBlockIndentation
                        };
                        edits.Add(edit);
                    }
                }
            }
        }

        private void FormatCodeBlockEnd(FormattingContext context, SourceText changedText, RazorDirectiveBodySyntax directiveBody, SyntaxNode innerCodeBlock, List<TextEdit> edits)
        {
            var sourceText = context.SourceText;
            var originalBodySpan = TextSpan.FromBounds(directiveBody.Position, directiveBody.EndPosition);
            var originalBodyRange = originalBodySpan.AsRange(sourceText);
            if (context.Range.End.Line < originalBodyRange.End.Line)
            {
                return;
            }

            // Last line is within the selected range. Let's try and format the end.

            TrackChangeInSpan(sourceText, originalBodySpan, changedText, out var changedBodySpan, out _);
            var changedBodyRange = changedBodySpan.AsRange(changedText);

            var firstLine = changedText.Lines[(int)changedBodyRange.Start.Line];
            var desiredIndentationLevel = context.Indentations[firstLine.LineNumber].IndentationLevel;
            var desiredIndentation = GetIndentationString(context, desiredIndentationLevel);

            // we want to keep the close '}' on its own line. So bring it to the next line.
            var originalInnerCodeBlockSpan = TextSpan.FromBounds(innerCodeBlock.Position, innerCodeBlock.EndPosition);
            TrackChangeInSpan(sourceText, originalInnerCodeBlockSpan, changedText, out var changedInnerCodeBlockSpan, out _);
            var closeCurlyLocation = changedInnerCodeBlockSpan.End;
            var closeCurlyLine = changedText.Lines.GetLineFromPosition(closeCurlyLocation);
            var firstNonWhitespaceOffset = closeCurlyLine.GetFirstNonWhitespaceOffset() ?? 0;
            if (closeCurlyLine.Start + firstNonWhitespaceOffset != closeCurlyLocation)
            {
                // This means the '}' is on the same line as some C# code.
                // Bring it down to the next line and apply the desired indentation.
                var edit = new TextEdit()
                {
                    Range = new Range(
                        new Position(closeCurlyLine.LineNumber, closeCurlyLocation - closeCurlyLine.Start),
                        new Position(closeCurlyLine.LineNumber, closeCurlyLocation - closeCurlyLine.Start)),
                    NewText = Environment.NewLine + desiredIndentation
                };
                edits.Add(edit);
            }
            else if (firstNonWhitespaceOffset != desiredIndentation.Length)
            {
                // This means the '}' is on its own line but is not indented correctly. Correct it.
                var edit = new TextEdit()
                {
                    Range = new Range(
                    new Position(closeCurlyLine.LineNumber, 0),
                    new Position(closeCurlyLine.LineNumber, firstNonWhitespaceOffset)),
                    NewText = desiredIndentation
                };
                edits.Add(edit);
            }
        }

        private SourceText ApplyChanges(SourceText original, IEnumerable<TextChange> changes)
        {
            var changed = original.WithChanges(changes);
            return changed;
        }

        private void TrackChangeInSpan(SourceText oldText, TextSpan originalSpan, SourceText newText, out TextSpan changedSpan, out TextSpan changeEncompassingSpan)
        {
            var affectedRange = newText.GetEncompassingTextChangeRange(oldText);
            changeEncompassingSpan = affectedRange.Span;
            if (!originalSpan.Contains(changeEncompassingSpan))
            {
                _logger.LogDebug($"The changed region {changeEncompassingSpan} was not a subset of the span {originalSpan} being tracked. This is unexpected.");
            }

            changedSpan = TextSpan.FromBounds(originalSpan.Start, originalSpan.End + affectedRange.NewLength - affectedRange.Span.Length);
        }

        private TextChange[] Diff(SourceText oldText, SourceText newText, TextSpan? spanToDiff = default)
        {
            // Once https://github.com/dotnet/roslyn/issues/41413 is fixed,
            // the following lines can be replaced with `newText.GetTextChanges(oldText)`.

            var spanToTrack = spanToDiff ?? TextSpan.FromBounds(0, oldText.Length);
            TrackChangeInSpan(oldText, spanToTrack, newText, out var changedSpanToTrack, out _);
            var change = new TextChange(spanToTrack, newText.GetSubText(changedSpanToTrack).ToString());
            return new[] { change };
        }

        private static TextSpan GetSpanIncludingPrecedingWhitespaceInLine(SourceText sourceText, int start, int end)
        {
            var line = sourceText.Lines.GetLineFromPosition(start);
            var precedingLineText = sourceText.GetSubTextString(TextSpan.FromBounds(line.Start, start));
            var precedingWhitespaceLength = precedingLineText.GetTrailingWhitespace().Length;

            return TextSpan.FromBounds(start - precedingWhitespaceLength, end);
        }

        private static string GetIndentationString(FormattingContext context, int indentationLevel)
        {
            var indentChar = context.Options.InsertSpaces ? ' ' : '\t';
            var indentation = new string(indentChar, (int)context.Options.TabSize * indentationLevel);
            return indentation;
        }

        private static FormattingContext CreateFormattingContext(Uri uri, RazorCodeDocument codedocument, Range range, FormattingOptions options)
        {
            var result = new FormattingContext()
            {
                Uri = uri,
                CodeDocument = codedocument,
                Range = range,
                Options = options
            };

            var source = codedocument.Source;
            var syntaxTree = codedocument.GetSyntaxTree();
            var formattingSpans = syntaxTree.GetFormattingSpans();

            var total = 0;
            var previousIndentationLevel = 0;
            for (var i = 0; i < source.Lines.Count; i++)
            {
                // Get first non-whitespace character position
                var lineLength = source.Lines.GetLineLength(i);
                var nonWsChar = 0;
                for (var j = 0; j < lineLength; j++)
                {
                    var ch = source[total + j];
                    if (!char.IsWhiteSpace(ch) && !ParserHelpers.IsNewLine(ch))
                    {
                        nonWsChar = j;
                        break;
                    }
                }

                // position now contains the first non-whitespace character or 0. Get the corresponding FormattingSpan.
                if (TryGetFormattingSpan(total + nonWsChar, formattingSpans, out var span))
                {
                    result.Indentations[i] = new IndentationContext
                    {
                        Line = i,
                        IndentationLevel = span.IndentationLevel,
                        RelativeIndentationLevel = span.IndentationLevel - previousIndentationLevel,
                        ExistingIndentation = nonWsChar,
                        FirstSpan = span,
                    };
                    previousIndentationLevel = span.IndentationLevel;
                }
                else
                {
                    // Couldn't find a corresponding FormattingSpan.
                    result.Indentations[i] = new IndentationContext
                    {
                        Line = i,
                        IndentationLevel = -1,
                        RelativeIndentationLevel = previousIndentationLevel,
                        ExistingIndentation = nonWsChar,
                    };
                }

                total += lineLength;
            }

            return result;
        }

        private static bool TryGetFormattingSpan(int absoluteIndex, IReadOnlyList<FormattingSpan> formattingspans, out FormattingSpan result)
        {
            result = null;
            for (var i = 0; i < formattingspans.Count; i++)
            {
                var formattingspan = formattingspans[i];
                var span = formattingspan.Span;

                if (span.Start <= absoluteIndex)
                {
                    if (span.End >= absoluteIndex)
                    {
                        if (span.End == absoluteIndex && span.Length > 0)
                        {
                            // We're at an edge.
                            // Non-marker spans (spans.length == 0) do not own the edges after it
                            continue;
                        }

                        result = formattingspan;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
