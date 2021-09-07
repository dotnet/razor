// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Extensions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class CSharpOnTypeFormattingPass : CSharpFormattingPassBase
    {
        private readonly ILogger _logger;

        public CSharpOnTypeFormattingPass(
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

            _logger = loggerFactory.CreateLogger<CSharpOnTypeFormattingPass>();
        }

        public async override Task<FormattingResult> ExecuteAsync(FormattingContext context, FormattingResult result, CancellationToken cancellationToken)
        {
            if (!context.IsFormatOnType || result.Kind != RazorLanguageKind.CSharp)
            {
                // We don't want to handle regular formatting or non-C# on type formatting here.
                return result;
            }

            // Normalize and re-map the C# edits.
            var codeDocument = context.CodeDocument;
            var csharpText = codeDocument.GetCSharpSourceText();
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
            var formattedText = ApplyChangesAndTrackChange(originalText, changes, out _, out var spanAfterFormatting);
            var changedContext = await context.WithTextAsync(formattedText);
            var rangeAfterFormatting = spanAfterFormatting.AsRange(formattedText);

            cancellationToken.ThrowIfCancellationRequested();

            // We make an optimistic attempt at fixing corner cases.
            var cleanupChanges = CleanupDocument(changedContext, rangeAfterFormatting);
            var cleanedText = formattedText.WithChanges(cleanupChanges);
            changedContext = await changedContext.WithTextAsync(cleanedText);

            cancellationToken.ThrowIfCancellationRequested();

            // At this point we should have applied all edits that adds/removes newlines.
            // Let's now ensure the indentation of each of those lines is correct.

            // We only want to adjust the range that was affected.
            // We need to take into account the lines affected by formatting as well as cleanup.
            var lineDelta = LineDelta(formattedText, cleanupChanges, out var firstPosition, out var lastPosition);

            // Okay hear me out, I know this looks lazy, but it totally makes sense.
            // This method is called with edits that the C# formatter wants to make, and from those edits we work out which
            // other edits to apply etc. Fine, all good so far. BUT its totally possible that the user typed a closing brace
            // in the same position as the C# formatter thought it should be, on the line _after_ the code that the C# formatter
            // reformatted.
            //
            // For example, given:
            // if (true){
            //     }
            //
            // If the C# formatter is happy with the placement of that close brace then this method will get two edits:
            //  * On line 1 to indent the if by 4 spaces
            //  * On line 1 to add a newline and 4 spaces in front of the opening brace
            //
            // We'll happy format lines 1 and 2, and ignore the closing brace altogether. So, by looking one line further
            // we won't have that problem.
            if (rangeAfterFormatting.End.Line + lineDelta < cleanedText.Lines.Count)
            {
                lineDelta++;
            }

            // Now we know how many lines were affected by the cleanup and formatting, but we don't know where those lines are. For example, given:
            //
            // @if (true)
            // {
            //      }
            // else
            // {
            // $$}
            //
            // When typing that close brace, the changes would fix the previous close brace, but the line delta would be 0, so
            // we'd format line 6 and call it a day, even though the foratter made an edit on line 3.
            var start = rangeAfterFormatting.Start;
            if (firstPosition is not null && firstPosition < start)
            {
                start = firstPosition;
            }

            var end = new Position(rangeAfterFormatting.End.Line + lineDelta, 0);
            if (lastPosition is not null && lastPosition < start)
            {
                end = lastPosition;
            }

            var rangeToAdjust = new Range(start, end);

            Debug.Assert(rangeToAdjust.End.IsValid(cleanedText), "Invalid range. This is unexpected.");

            var indentationChanges = await AdjustIndentationAsync(changedContext, cancellationToken, rangeToAdjust);
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

        // Returns the minimal TextSpan that encompasses all the differences between the old and the new text.
        private static SourceText ApplyChangesAndTrackChange(SourceText oldText, IEnumerable<TextChange> changes, out TextSpan spanBeforeChange, out TextSpan spanAfterChange)
        {
            if (oldText is null)
            {
                throw new ArgumentNullException(nameof(oldText));
            }

            if (changes is null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            var newText = oldText.WithChanges(changes);
            var affectedRange = newText.GetEncompassingTextChangeRange(oldText);

            spanBeforeChange = affectedRange.Span;
            spanAfterChange = new TextSpan(spanBeforeChange.Start, affectedRange.NewLength);

            return newText;
        }

        private TextEdit[] FilterCSharpTextEdits(FormattingContext context, TextEdit[] edits)
        {
            var filteredEdits = edits.Where(e =>
            {
                var span = e.Range.AsTextSpan(context.SourceText);
                return ShouldFormat(context, span, allowImplicitStatements: false);
            }).ToArray();

            return filteredEdits;
        }

        private static int LineDelta(SourceText text, IEnumerable<TextChange> changes, out Position? firstPosition, out Position? lastPosition)
        {
            firstPosition = null;
            lastPosition = null;

            // Let's compute the number of newlines added/removed by the incoming changes.
            var delta = 0;

            foreach (var change in changes)
            {
                var newLineCount = change.NewText is null ? 0 : change.NewText.Split('\n').Length - 1;

                var range = change.Span.AsRange(text);
                Debug.Assert(range.Start.Line <= range.End.Line, "Invalid range.");

                if (firstPosition is null || firstPosition > range.Start)
                {
                    firstPosition = range.Start;
                }
                if (lastPosition is null || lastPosition < range.End)
                {
                    lastPosition = range.End;
                }

                // The number of lines added/removed will be,
                // the number of lines added by the change  - the number of lines the change span represents
                delta += newLineCount - (range.End.Line - range.Start.Line);
            }

            return delta;
        }

        private static List<TextChange> CleanupDocument(FormattingContext context, Range? range = null)
        {
            var isOnType = range is not null;

            var text = context.SourceText;
            range ??= TextSpan.FromBounds(0, text.Length).AsRange(text);
            var csharpDocument = context.CodeDocument.GetCSharpDocument();

            var changes = new List<TextChange>();
            foreach (var mapping in csharpDocument.SourceMappings)
            {
                var mappingSpan = new TextSpan(mapping.OriginalSpan.AbsoluteIndex, mapping.OriginalSpan.Length);
                var mappingRange = mappingSpan.AsRange(text);
                if (!range.LineOverlapsWith(mappingRange))
                {
                    // We don't care about this range. It didn't change.
                    continue;
                }

                CleanupSourceMappingStart(context, mappingRange, changes, isOnType, out var newLineAdded);

                CleanupSourceMappingEnd(context, mappingRange, changes, newLineAdded);
            }

            return changes;
        }

        private static void CleanupSourceMappingStart(FormattingContext context, Range sourceMappingRange, List<TextChange> changes, bool isOnType, out bool newLineAdded)
        {
            newLineAdded = false;

            //
            // We look through every source mapping that intersects with the affected range and
            // bring the first line to its own line and adjust its indentation,
            //
            // E.g,
            //
            // @{   public int x = 0;
            // }
            //
            // becomes,
            //
            // @{
            //    public int x  = 0;
            // }
            //

            var text = context.SourceText;
            var sourceMappingSpan = sourceMappingRange.AsTextSpan(text);
            if (!ShouldFormat(context, sourceMappingSpan, allowImplicitStatements: false))
            {
                // We don't want to run cleanup on this range.
                return;
            }

            if (sourceMappingRange.Start.Character == 0)
            {
                // It already starts on a fresh new line which doesn't need cleanup.
                // E.g, (The mapping starts at | in the below case)
                // @{
                //     @: Some html
                // |   var x = 123;
                // }
                //

                return;
            }

            // @{
            //     if (true)
            //     {
            //         <div></div>|
            //
            //              |}
            // }
            // We want to return the length of the range marked by |...|
            //
            var whitespaceLength = text.GetFirstNonWhitespaceOffset(sourceMappingSpan, out var newLineCount);
            if (whitespaceLength == null)
            {
                // There was no content after the start of this mapping. Meaning it already is clean.
                // E.g,
                // @{|
                //    ...
                // }

                return;
            }

            var spanToReplace = new TextSpan(sourceMappingSpan.Start, whitespaceLength.Value);
            if (!context.TryGetIndentationLevel(spanToReplace.End, out var contentIndentLevel))
            {
                // Can't find the correct indentation for this content. Leave it alone.
                return;
            }

            if (newLineCount == 0)
            {
                newLineAdded = true;
                newLineCount = 1;
            }

            // At this point, `contentIndentLevel` should contain the correct indentation level for `}` in the above example.
            // Make sure to preserve the same number of blank lines as the original string had
            var replacement = PrependLines(context.GetIndentationLevelString(contentIndentLevel), context.NewLineString, newLineCount);

            // After the below change the above example should look like,
            // @{
            //     if (true)
            //     {
            //         <div></div>
            //     }
            // }
            var change = new TextChange(spanToReplace, replacement);
            changes.Add(change);
        }

        private static string PrependLines(string text, string newLine, int count)
        {
            var builder = new StringBuilder((newLine.Length * count) + text.Length);
            for (var i = 0; i < count; i++)
            {
                builder.Append(newLine);
            }

            builder.Append(text);
            return builder.ToString();
        }

        private static void CleanupSourceMappingEnd(FormattingContext context, Range sourceMappingRange, List<TextChange> changes, bool newLineWasAddedAtStart)
        {
            //
            // We look through every source mapping that intersects with the affected range and
            // bring the content after the last line to its own line and adjust its indentation,
            //
            // E.g,
            //
            // @{
            //     if (true)
            //     {  <div></div>
            //     }
            // }
            //
            // becomes,
            //
            // @{
            //    if (true)
            //    {
            //        </div></div>
            //    }
            // }
            //

            var text = context.SourceText;
            var sourceMappingSpan = sourceMappingRange.AsTextSpan(text);
            var mappingEndLineIndex = sourceMappingRange.End.Line;

            var startsInCSharpContext = context.Indentations[mappingEndLineIndex].StartsInCSharpContext;

            // If the span is on a single line, and we added a line, then end point is now on a line that does start in a C# context.
            if (!startsInCSharpContext && newLineWasAddedAtStart && sourceMappingRange.Start.Line == mappingEndLineIndex)
            {
                startsInCSharpContext = true;
            }

            if (!startsInCSharpContext)
            {
                // For corner cases like (Position marked with |),
                // It is already in a separate line. It doesn't need cleaning up.
                // @{
                //     if (true}
                //     {
                //         |<div></div>
                //     }
                // }
                //
                return;
            }

            var endSpan = TextSpan.FromBounds(sourceMappingSpan.End, sourceMappingSpan.End);
            if (!ShouldFormat(context, endSpan, allowImplicitStatements: false))
            {
                // We don't want to run cleanup on this range.
                return;
            }

            var contentStartOffset = text.Lines[mappingEndLineIndex].GetFirstNonWhitespaceOffset(sourceMappingRange.End.Character);
            if (contentStartOffset == null)
            {
                // There is no content after the end of this source mapping. No need to clean up.
                return;
            }

            var spanToReplace = new TextSpan(sourceMappingSpan.End, 0);
            if (!context.TryGetIndentationLevel(spanToReplace.End, out var contentIndentLevel))
            {
                // Can't find the correct indentation for this content. Leave it alone.
                return;
            }

            // At this point, `contentIndentLevel` should contain the correct indentation level for `}` in the above example.
            var replacement = context.NewLineString + context.GetIndentationLevelString(contentIndentLevel);

            // After the below change the above example should look like,
            // @{
            //     if (true)
            //     {
            //         <div></div>
            //     }
            // }
            var change = new TextChange(spanToReplace, replacement);
            changes.Add(change);
        }
    }
}
