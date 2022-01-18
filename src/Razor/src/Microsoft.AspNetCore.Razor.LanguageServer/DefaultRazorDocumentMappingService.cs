// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Microsoft.Extensions.Logging;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultRazorDocumentMappingService : RazorDocumentMappingService
    {
        private readonly ILogger _logger;

        public DefaultRazorDocumentMappingService(ILoggerFactory loggerFactory) : base()
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<DefaultRazorDocumentMappingService>();
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        [Obsolete("This only exists to prevent Moq from complaining, use the other constructor.")]
        public DefaultRazorDocumentMappingService() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public override TextEdit[] GetProjectedDocumentEdits(RazorCodeDocument codeDocument, TextEdit[] edits)
        {
            var projectedEdits = new List<TextEdit>();
            var csharpSourceText = codeDocument.GetCSharpSourceText();
            var lastNewLineAddedToLine = 0;

            foreach (var edit in edits)
            {
                var range = edit.Range;
                if (!IsRangeWithinDocument(range, csharpSourceText))
                {
                    continue;
                }

                var startSync = range.Start.TryGetAbsoluteIndex(csharpSourceText, _logger, out var startIndex);
                var endSync = range.End.TryGetAbsoluteIndex(csharpSourceText, _logger, out var endIndex);
                if (startSync is false || endSync is false)
                {
                    break;
                }

                var mappedStart = TryMapFromProjectedDocumentPosition(codeDocument, startIndex, out var hostDocumentStart, out _);
                var mappedEnd = TryMapFromProjectedDocumentPosition(codeDocument, endIndex, out var hostDocumentEnd, out _);

                // Ideal case, both start and end can be mapped so just return the edit
                if (mappedStart && mappedEnd)
                {
                    // If the previous edit was on the same line, and added a newline, then we need to add a space
                    // between this edit and the previous one, because the normalization will have swallowed it. See
                    // below for a more info.
                    var newText = (lastNewLineAddedToLine == range.Start.Line ? " " : "") + edit.NewText;
                    projectedEdits.Add(new TextEdit()
                    {
                        NewText = newText,
                        Range = new Range(hostDocumentStart!, hostDocumentEnd!)
                    });
                    continue;
                }

                // For the first line of a code block the C# formatter will often return an edit that starts
                // before our mapping, but ends within. In those cases, when the edit spans multiple lines
                // we just take the last line and try to use that.
                //
                // eg in the C# document you might see:
                //
                //      protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
                //      {
                // #nullable restore
                // #line 1 "/path/to/Document.component"
                //
                //          var x = DateTime.Now;
                //
                // To indent the 'var x' line the formatter will return an edit that starts the line before,
                // with a NewText of '\n            '. The start of that edit is outside our mapping, but we
                // still want to know how to format the 'var x' line, so we have to break up the edit.
                if (!mappedStart && mappedEnd && range.SpansMultipleLines())
                {
                    // Construct a theoretical edit that is just for the last line of the edit that the C# formatter
                    // gave us, and see if we can map that.
                    // The +1 here skips the newline character that is found, but also protects from Substring throwing
                    // if there are no newlines (which should be impossible anyway)
                    var lastNewLine = edit.NewText.LastIndexOfAny(new char[] { '\n', '\r' }) + 1;

                    // Strictly speaking we could be dropping more lines than we need to, because our mapping point could be anywhere within the edit
                    // but we know that the C# formatter will only be returning blank lines up until the first bit of content that needs to be indented
                    // so we can ignore all but the last line. This assert ensures that is true, just in case something changes in Roslyn
                    Debug.Assert(lastNewLine == 0 || edit.NewText.Substring(0, lastNewLine - 1).All(c => c == '\r' || c == '\n'), "We are throwing away part of an edit that has more than just empty lines!");

                    var proposedRange = new Range(range.End.Line, 0, range.End.Line, range.End.Character);
                    startSync = proposedRange.Start.TryGetAbsoluteIndex(csharpSourceText, _logger, out startIndex);
                    endSync = proposedRange.End.TryGetAbsoluteIndex(csharpSourceText, _logger, out endIndex);
                    if (startSync is false || endSync is false)
                    {
                        break;
                    }

                    mappedStart = TryMapFromProjectedDocumentPosition(codeDocument, startIndex, out hostDocumentStart, out _);
                    mappedEnd = TryMapFromProjectedDocumentPosition(codeDocument, endIndex, out hostDocumentEnd, out _);

                    if (mappedStart && mappedEnd)
                    {
                        projectedEdits.Add(new TextEdit()
                        {
                            NewText = edit.NewText.Substring(lastNewLine),
                            Range = new Range(hostDocumentStart!, hostDocumentEnd!)
                        });
                        continue;
                    }
                }

                // If we couldn't map either the start or the end then we still might want to do something tricky.
                // When we have a block like this:
                //
                // @functions {
                //    class Goo
                //    {
                //    }
                // }
                //
                // The source mapping starts at char 13 on the "@functions" line (after the open brace). Unfortunately
                // and code that is needed on that line, say an attribute that the code action wants to insert, will
                // start at char 8 because of the desired indentation of that new code. This means it starts outside of the
                // mapping, so is thrown away, which results in data loss.
                //
                // To fix this we check and if the mapping would have been successful at the end of the line (char 13 above)
                // then we insert a newline, and enough indentation to get us back out to where the new code wanted to start (char 8)
                // and then we're good - we've left the @functions bit alone which razor needs, but we're still able to insert
                // new code above where the C# code is in the generated document.
                //
                // One last hurdle is that sometimes these edits come in as separate edits. So for example replacing "class Goo" above
                // with "public class Goo" would come in as one edit for "public", one for "class" and one for "Goo", all on the same line.
                // When we map the edit for "public" we will push everything down a line, so we don't want to do it for other edits
                // on that line.
                if (!mappedStart && !mappedEnd && !range.SpansMultipleLines())
                {
                    // If the new text doesn't have any content we don't care - throwing away invisible whitespace is fine
                    if (string.IsNullOrWhiteSpace(edit.NewText))
                    {
                        continue;
                    }

                    var line = csharpSourceText.Lines[range.Start.Line];

                    // If the line isn't blank, then this isn't a functions directive
                    if (line.GetFirstNonWhitespaceOffset() is not null)
                    {
                        continue;
                    }

                    // Only do anything if the end of the line in question is a valid mapping point (ie, a transition)
                    var endOfLine = line.Span.End;
                    if (TryMapFromProjectedDocumentPosition(codeDocument, endOfLine, out var hostDocumentIndex, out _))
                    {
                        if (range.Start.Line == lastNewLineAddedToLine)
                        {
                            // If we already added a newline to this line, then we don't want to add another one, but
                            // we do need to add a space between this edit and the previous one, because the normalization
                            // will have swallowed it.
                            projectedEdits.Add(new TextEdit()
                            {
                                NewText = " " + edit.NewText,
                                Range = new Range(hostDocumentIndex, hostDocumentIndex)
                            });
                        }
                        else
                        {
                            // Otherwise, add a newline and the real content, and remember where we added it
                            lastNewLineAddedToLine = range.Start.Line;
                            projectedEdits.Add(new TextEdit()
                            {
                                NewText = Environment.NewLine + new string(' ', range.Start.Character) + edit.NewText,
                                Range = new Range(hostDocumentIndex, hostDocumentIndex)
                            });
                        }

                        continue;
                    }
                }
            }

            return projectedEdits.ToArray();
        }

        public override bool TryMapFromProjectedDocumentRange(RazorCodeDocument codeDocument, Range projectedRange, [NotNullWhen(true)] out Range? originalRange) => TryMapFromProjectedDocumentRange(codeDocument, projectedRange, MappingBehavior.Strict, out originalRange);

        public override bool TryMapFromProjectedDocumentRange(RazorCodeDocument codeDocument, Range projectedRange, MappingBehavior mappingBehavior, [NotNullWhen(true)] out Range? originalRange)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            if (projectedRange is null)
            {
                throw new ArgumentNullException(nameof(projectedRange));
            }

            if (mappingBehavior == MappingBehavior.Strict)
            {
                return TryMapFromProjectedDocumentRangeStrict(codeDocument, projectedRange, out originalRange);
            }
            else if (mappingBehavior == MappingBehavior.Inclusive)
            {
                return TryMapFromProjectedDocumentRangeInclusive(codeDocument, projectedRange, out originalRange);
            }
            else
            {
                throw new InvalidOperationException(RazorLS.Resources.Unknown_mapping_behavior);
            }
        }

        public override bool TryMapToProjectedDocumentRange(RazorCodeDocument codeDocument, Range originalRange, [NotNullWhen(true)] out Range? projectedRange)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            if (originalRange is null)
            {
                throw new ArgumentNullException(nameof(originalRange));
            }

            projectedRange = default;

            if ((originalRange.End.Line < originalRange.Start.Line) ||
                (originalRange.End.Line == originalRange.Start.Line &&
                 originalRange.End.Character < originalRange.Start.Character))
            {
                Debug.Fail($"DefaultRazorDocumentMappingService:TryMapToProjectedDocumentRange original range end < start '{originalRange}'");
                return false;
            }

            var sourceText = codeDocument.GetSourceText();
            var range = originalRange;
            if (!IsRangeWithinDocument(range, sourceText))
            {
                return false;
            }

            if (!range.Start.TryGetAbsoluteIndex(sourceText, _logger, out var startIndex) ||
                !TryMapToProjectedDocumentPosition(codeDocument, startIndex, out var projectedStart, out var _))
            {
                return false;
            }

            if (!range.End.TryGetAbsoluteIndex(sourceText, _logger, out var endIndex) ||
                !TryMapToProjectedDocumentPosition(codeDocument, endIndex, out var projectedEnd, out var _))
            {
                return false;
            }

            // Ensures a valid range is returned.
            // As we're doing two separate TryMapToProjectedDocumentPosition calls,
            // it's possible the projectedStart and projectedEnd positions are in completely
            // different places in the document, including the possibility that the
            // projectedEnd position occurs before the projectedStart position.
            // We explicitly disallow such ranges where the end < start.
            if ((projectedEnd.Line < projectedStart.Line) ||
                (projectedEnd.Line == projectedStart.Line &&
                 projectedEnd.Character < projectedStart.Character))
            {
                return false;
            }

            projectedRange = new Range(
                projectedStart,
                projectedEnd);

            return true;
        }

        public override bool TryMapFromProjectedDocumentPosition(RazorCodeDocument codeDocument, int csharpAbsoluteIndex, [NotNullWhen(true)] out Position? originalPosition, out int originalIndex)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            var csharpDoc = codeDocument.GetCSharpDocument();
            foreach (var mapping in csharpDoc.SourceMappings)
            {
                var generatedSpan = mapping.GeneratedSpan;
                var generatedAbsoluteIndex = generatedSpan.AbsoluteIndex;
                if (generatedAbsoluteIndex <= csharpAbsoluteIndex)
                {
                    // Treat the mapping as owning the edge at its end (hence <= originalSpan.Length),
                    // otherwise we wouldn't handle the cursor being right after the final C# char
                    var distanceIntoGeneratedSpan = csharpAbsoluteIndex - generatedAbsoluteIndex;
                    if (distanceIntoGeneratedSpan <= generatedSpan.Length)
                    {
                        // Found the generated span that contains the csharp absolute index

                        originalIndex = mapping.OriginalSpan.AbsoluteIndex + distanceIntoGeneratedSpan;
                        var originalLocation = codeDocument.Source.Lines.GetLocation(originalIndex);
                        originalPosition = new Position(originalLocation.LineIndex, originalLocation.CharacterIndex);
                        return true;
                    }
                }
            }

            originalPosition = default;
            originalIndex = default;
            return false;
        }

        public override bool TryMapToProjectedDocumentPosition(RazorCodeDocument codeDocument, int absoluteIndex, [NotNullWhen(true)] out Position? projectedPosition, out int projectedIndex)
            => TryMapToProjectedDocumentPositionInternal(codeDocument, absoluteIndex, nextCSharpPositionOnFailure: false, out projectedPosition, out projectedIndex);

        public override bool TryMapToProjectedDocumentOrNextCSharpPosition(RazorCodeDocument codeDocument, int absoluteIndex, [NotNullWhen(true)] out Position? projectedPosition, out int projectedIndex)
            => TryMapToProjectedDocumentPositionInternal(codeDocument, absoluteIndex, nextCSharpPositionOnFailure: true, out projectedPosition, out projectedIndex);

        private static bool TryMapToProjectedDocumentPositionInternal(RazorCodeDocument codeDocument, int absoluteIndex, bool nextCSharpPositionOnFailure, [NotNullWhen(true)] out Position? projectedPosition, out int projectedIndex)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            var csharpDoc = codeDocument.GetCSharpDocument();
            foreach (var mapping in csharpDoc.SourceMappings)
            {
                var originalSpan = mapping.OriginalSpan;
                var originalAbsoluteIndex = originalSpan.AbsoluteIndex;
                if (originalAbsoluteIndex <= absoluteIndex)
                {
                    // Treat the mapping as owning the edge at its end (hence <= originalSpan.Length),
                    // otherwise we wouldn't handle the cursor being right after the final C# char
                    var distanceIntoOriginalSpan = absoluteIndex - originalAbsoluteIndex;
                    if (distanceIntoOriginalSpan <= originalSpan.Length)
                    {
                        projectedIndex = mapping.GeneratedSpan.AbsoluteIndex + distanceIntoOriginalSpan;
                        projectedPosition = GetProjectedPosition(codeDocument, projectedIndex);
                        return true;
                    }
                }
                else if (nextCSharpPositionOnFailure)
                {
                    codeDocument.GetSourceText().GetLineAndOffset(absoluteIndex, out var hostDocumentLine, out _);
                    if (mapping.OriginalSpan.LineIndex == hostDocumentLine)
                    {
                        projectedIndex = mapping.GeneratedSpan.AbsoluteIndex;
                        projectedPosition = GetProjectedPosition(codeDocument, projectedIndex);
                        return true;
                    }

                    break;
                }
            }

            projectedPosition = default;
            projectedIndex = default;
            return false;

            static Position GetProjectedPosition(RazorCodeDocument codeDocument, int projectedIndex)
            {
                var generatedSource = codeDocument.GetCSharpSourceText();
                var generatedLinePosition = generatedSource.Lines.GetLinePosition(projectedIndex);
                var projectedPosition = new Position(generatedLinePosition.Line, generatedLinePosition.Character);
                return projectedPosition;
            }
        }

        public override RazorLanguageKind GetLanguageKind(RazorCodeDocument codeDocument, int originalIndex)
        {
            if (codeDocument is null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            var syntaxTree = codeDocument.GetSyntaxTree();
            var classifiedSpans = syntaxTree.GetClassifiedSpans();
            var tagHelperSpans = syntaxTree.GetTagHelperSpans();
            var documentLength = codeDocument.GetSourceText().Length;
            var languageKind = GetLanguageKindCore(classifiedSpans, tagHelperSpans, originalIndex, documentLength);

            return languageKind;
        }

        // Internal for testing
        internal static RazorLanguageKind GetLanguageKindCore(
            IReadOnlyList<ClassifiedSpanInternal> classifiedSpans,
            IReadOnlyList<TagHelperSpanInternal> tagHelperSpans,
            int absoluteIndex,
            int documentLength)
        {
            for (var i = 0; i < classifiedSpans.Count; i++)
            {
                var classifiedSpan = classifiedSpans[i];
                var span = classifiedSpan.Span;

                if (span.AbsoluteIndex <= absoluteIndex)
                {
                    var end = span.AbsoluteIndex + span.Length;
                    if (end >= absoluteIndex)
                    {
                        if (end == absoluteIndex)
                        {
                            // We're at an edge.

                            if (span.Length > 0 &&
                                classifiedSpan.AcceptedCharacters == AcceptedCharactersInternal.None)
                            {
                                // Non-marker spans do not own the edges after it
                                continue;
                            }
                        }

                        return GetLanguageFromClassifiedSpan(classifiedSpan);
                    }
                }
            }

            for (var i = 0; i < tagHelperSpans.Count; i++)
            {
                var tagHelperSpan = tagHelperSpans[i];
                var span = tagHelperSpan.Span;

                if (span.AbsoluteIndex <= absoluteIndex)
                {
                    var end = span.AbsoluteIndex + span.Length;
                    if (end >= absoluteIndex)
                    {
                        if (end == absoluteIndex)
                        {
                            // We're at an edge. TagHelper spans never own their edge and aren't represented by marker spans
                            continue;
                        }

                        // Found intersection
                        return RazorLanguageKind.Html;
                    }
                }
            }

            // Use the language of the last classified span if we're at the end
            // of the document.
            if (classifiedSpans.Count != 0 && absoluteIndex == documentLength)
            {
                var lastClassifiedSpan = classifiedSpans.Last();
                return GetLanguageFromClassifiedSpan(lastClassifiedSpan);
            }

            // Default to Razor
            return RazorLanguageKind.Razor;

            static RazorLanguageKind GetLanguageFromClassifiedSpan(ClassifiedSpanInternal classifiedSpan)
            {
                // Overlaps with request
                return classifiedSpan.SpanKind switch
                {
                    SpanKindInternal.Markup => RazorLanguageKind.Html,
                    SpanKindInternal.Code => RazorLanguageKind.CSharp,

                    // Content type was non-C# or Html or we couldn't find a classified span overlapping the request position.
                    // All other classified span kinds default back to Razor
                    _ => RazorLanguageKind.Razor,
                };
            }
        }

        private bool TryMapFromProjectedDocumentRangeStrict(RazorCodeDocument codeDocument, Range projectedRange, out Range? originalRange)
        {
            originalRange = default;

            var csharpSourceText = codeDocument.GetCSharpSourceText();
            var range = projectedRange;
            if (!IsRangeWithinDocument(range, csharpSourceText))
            {
                return false;
            }

            if (!range.Start.TryGetAbsoluteIndex(csharpSourceText, _logger, out var startIndex) ||
                !TryMapFromProjectedDocumentPosition(codeDocument, startIndex, out var hostDocumentStart, out _))
            {
                return false;
            }

            if (!range.End.TryGetAbsoluteIndex(csharpSourceText, _logger, out var endIndex) ||
                !TryMapFromProjectedDocumentPosition(codeDocument, endIndex, out var hostDocumentEnd, out _))
            {
                return false;
            }

            originalRange = new Range(
                hostDocumentStart,
                hostDocumentEnd);

            return true;
        }

        private bool TryMapFromProjectedDocumentRangeInclusive(RazorCodeDocument codeDocument, Range projectedRange, out Range? originalRange)
        {
            originalRange = default;

            var csharpDoc = codeDocument.GetCSharpDocument();
            var csharpSourceText = codeDocument.GetCSharpSourceText();
            var projectedRangeAsSpan = projectedRange.AsTextSpan(csharpSourceText);
            var range = projectedRange;
            var startIndex = projectedRangeAsSpan.Start;
            var startMappedDirectly = TryMapFromProjectedDocumentPosition(codeDocument, startIndex, out var hostDocumentStart, out _);

            var endIndex = projectedRangeAsSpan.End;
            var endMappedDirectly = TryMapFromProjectedDocumentPosition(codeDocument, endIndex, out var hostDocumentEnd, out _);

            if (startMappedDirectly && endMappedDirectly)
            {
                // We strictly mapped the start/end of the projected range.
                originalRange = new Range(hostDocumentStart!, hostDocumentEnd!);
                return true;
            }

            List<SourceMapping> candidateMappings;
            if (startMappedDirectly)
            {
                // Start of projected range intersects with a mapping
                candidateMappings = csharpDoc.SourceMappings.Where(mapping => IntersectsWith(startIndex, mapping.GeneratedSpan)).ToList();
            }
            else if (endMappedDirectly)
            {
                // End of projected range intersects with a mapping
                candidateMappings = csharpDoc.SourceMappings.Where(mapping => IntersectsWith(endIndex, mapping.GeneratedSpan)).ToList();
            }
            else
            {
                // Our range does not intersect with any mapping; we should see if it overlaps generated locations
                candidateMappings = csharpDoc.SourceMappings.Where(mapping => Overlaps(projectedRangeAsSpan, mapping.GeneratedSpan)).ToList();
            }

            if (candidateMappings.Count == 1)
            {
                // We're intersecting or overlapping a single mapping, lets choose that.

                var mapping = candidateMappings[0];
                originalRange = ConvertMapping(codeDocument.Source, mapping);
                return true;
            }
            else
            {
                // More then 1 or exactly 0 intersecting/overlapping mappings
                return false;
            }

            bool Overlaps(TextSpan projectedRangeAsSpan, SourceSpan span)
            {
                var overlapStart = Math.Max(projectedRangeAsSpan.Start, span.AbsoluteIndex);
                var overlapEnd = Math.Min(projectedRangeAsSpan.End, span.AbsoluteIndex + span.Length);

                return overlapStart < overlapEnd;
            }

            bool IntersectsWith(int position, SourceSpan span)
            {
                return unchecked((uint)(position - span.AbsoluteIndex) <= (uint)span.Length);
            }

            static Range ConvertMapping(RazorSourceDocument sourceDocument, SourceMapping mapping)
            {
                var startLocation = sourceDocument.Lines.GetLocation(mapping.OriginalSpan.AbsoluteIndex);
                var endLocation = sourceDocument.Lines.GetLocation(mapping.OriginalSpan.AbsoluteIndex + mapping.OriginalSpan.Length);
                var convertedRange = new Range(
                    new Position(startLocation.LineIndex, startLocation.CharacterIndex),
                    new Position(endLocation.LineIndex, endLocation.CharacterIndex));
                return convertedRange;
            }
        }

        private static bool s_haveAsserted = false;

        private static bool IsRangeWithinDocument(Range range, SourceText sourceText)
        {
            // This might happen when the document that ranges were created against was not the same as the document we're consulting.
            var result = IsPositionWithinDocument(range.Start, sourceText) && IsPositionWithinDocument(range.End, sourceText);

            if (!s_haveAsserted && !result)
            {
                s_haveAsserted = true;
                Debug.Fail($"Attempted to map a range {range} outside of the Source (line count {sourceText.Lines.Count}.) This could happen if the Roslyn and Razor LSP servers are not in sync.");
            }

            return result;

            static bool IsPositionWithinDocument(Position position, SourceText sourceText)
            {
                return position.Line < sourceText.Lines.Count;
            }
        }
    }
}
