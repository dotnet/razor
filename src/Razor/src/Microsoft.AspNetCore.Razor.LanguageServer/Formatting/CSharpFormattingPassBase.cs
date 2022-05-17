﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using TextSpan = Microsoft.CodeAnalysis.Text.TextSpan;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal abstract class CSharpFormattingPassBase : FormattingPassBase
    {
        protected CSharpFormattingPassBase(RazorDocumentMappingService documentMappingService, FilePathNormalizer filePathNormalizer, ClientNotifierServiceBase server)
            : base(documentMappingService, filePathNormalizer, server)
        {
            CSharpFormatter = new CSharpFormatter(documentMappingService, server, filePathNormalizer);
        }

        protected CSharpFormatter CSharpFormatter { get; }

        public override bool IsValidationPass => false;

        protected async Task<List<TextChange>> AdjustIndentationAsync(FormattingContext context, CancellationToken cancellationToken, Range? range = null)
        {
            // In this method, the goal is to make final adjustments to the indentation of each line.
            // We will take into account the following,
            // 1. The indentation due to nested C# structures
            // 2. The indentation due to Razor and HTML constructs

            var text = context.SourceText;
            range ??= TextSpan.FromBounds(0, text.Length).AsRange(text);

            // To help with figuring out the correct indentation, first we will need the indentation
            // that the C# formatter wants to apply in the following locations,
            // 1. The start and end of each of our source mappings
            // 2. The start of every line that starts in C# context

            // Due to perf concerns, we only want to invoke the real C# formatter once.
            // So, let's collect all the significant locations that we want to obtain the CSharpDesiredIndentations for.

            var significantLocations = new HashSet<int>();

            // First, collect all the locations at the beginning and end of each source mapping.
            var sourceMappingMap = new Dictionary<int, int>();
            foreach (var mapping in context.CodeDocument.GetCSharpDocument().SourceMappings)
            {
                var mappingSpan = new TextSpan(mapping.OriginalSpan.AbsoluteIndex, mapping.OriginalSpan.Length);
                if (!ShouldFormat(context, mappingSpan, allowImplicitStatements: true))
                {
                    // We don't care about this range as this can potentially lead to incorrect scopes.
                    continue;
                }

                var originalStartLocation = mapping.OriginalSpan.AbsoluteIndex;
                var projectedStartLocation = mapping.GeneratedSpan.AbsoluteIndex;
                sourceMappingMap[originalStartLocation] = projectedStartLocation;
                significantLocations.Add(projectedStartLocation);

                var originalEndLocation = mapping.OriginalSpan.AbsoluteIndex + mapping.OriginalSpan.Length + 1;
                var projectedEndLocation = mapping.GeneratedSpan.AbsoluteIndex + mapping.GeneratedSpan.Length + 1;
                sourceMappingMap[originalEndLocation] = projectedEndLocation;
                significantLocations.Add(projectedEndLocation);
            }

            // Next, collect all the line starts that start in C# context
            var indentations = context.GetIndentations();
            var lineStartMap = new Dictionary<int, int>();
            for (var i = range.Start.Line; i <= range.End.Line; i++)
            {
                if (indentations[i].EmptyOrWhitespaceLine)
                {
                    // We should remove whitespace on empty lines.
                    continue;
                }

                var line = context.SourceText.Lines[i];
                var lineStart = line.GetFirstNonWhitespacePosition() ?? line.Start;

                var lineStartSpan = new TextSpan(lineStart, 0);
                if (!ShouldFormat(context, lineStartSpan, allowImplicitStatements: true))
                {
                    // We don't care about this range as this can potentially lead to incorrect scopes.
                    continue;
                }

                if (DocumentMappingService.TryMapToProjectedDocumentPosition(context.CodeDocument, lineStart, out _, out var projectedLineStart))
                {
                    lineStartMap[lineStart] = projectedLineStart;
                    significantLocations.Add(projectedLineStart);
                }
            }

            // Now, invoke the C# formatter to obtain the CSharpDesiredIndentation for all significant locations.
            var significantLocationIndentation = await CSharpFormatter.GetCSharpIndentationAsync(context, significantLocations, cancellationToken);

            // Build source mapping indentation scopes.
            var sourceMappingIndentations = new SortedDictionary<int, int>();
            foreach (var originalLocation in sourceMappingMap.Keys)
            {
                var significantLocation = sourceMappingMap[originalLocation];
                if (!significantLocationIndentation.TryGetValue(significantLocation, out var indentation))
                {
                    // C# formatter didn't return an indentation for this. Skip.
                    continue;
                }

                sourceMappingIndentations[originalLocation] = indentation;
            }

            var sourceMappingIndentationScopes = sourceMappingIndentations.Keys.ToArray();

            // Build lineStart indentation map.
            var lineStartIndentations = new Dictionary<int, int>();
            foreach (var originalLocation in lineStartMap.Keys)
            {
                var significantLocation = lineStartMap[originalLocation];
                if (!significantLocationIndentation.TryGetValue(significantLocation, out var indentation))
                {
                    // C# formatter didn't return an indentation for this. Skip.
                    continue;
                }

                lineStartIndentations[originalLocation] = indentation;
            }

            // Now, let's combine the C# desired indentation with the Razor and HTML indentation for each line.
            var newIndentations = new Dictionary<int, int>();
            for (var i = range.Start.Line; i <= range.End.Line; i++)
            {
                if (indentations[i].EmptyOrWhitespaceLine)
                {
                    // We should remove whitespace on empty lines.
                    newIndentations[i] = 0;
                    continue;
                }

                var minCSharpIndentation = context.GetIndentationOffsetForLevel(indentations[i].MinCSharpIndentLevel);
                var line = context.SourceText.Lines[i];
                var lineStart = line.GetFirstNonWhitespacePosition() ?? line.Start;
                var lineStartSpan = new TextSpan(lineStart, 0);
                if (!ShouldFormat(context, lineStartSpan, allowImplicitStatements: true))
                {
                    // We don't care about this line as it lies in an area we don't want to format.
                    continue;
                }

                if (!lineStartIndentations.TryGetValue(lineStart, out var csharpDesiredIndentation))
                {
                    // Couldn't remap. This is probably a non-C# location.
                    // Use SourceMapping indentations to locate the C# scope of this line.
                    // E.g,
                    //
                    // @if (true) {
                    //   <div>
                    //  |</div>
                    // }
                    //
                    // We can't find a direct mapping at |, but we can infer its base indentation from the
                    // indentation of the latest source mapping prior to this line.
                    // We use binary search to find that spot.

                    var index = Array.BinarySearch(sourceMappingIndentationScopes, lineStart);

                    if (index < 0 && context.SourceText[lineStart] == '@')
                    {
                        // Sometimes we are only off by one in finding a source mapping, for example with a simple if statement:
                        //
                        // @|if (true)
                        //
                        // The sourceMappingIndentationScopes knows about where the pipe is (ie, after the "@") but we're asking
                        // for indentation at the line start. In these cases we are better off using the real indentation scope,
                        // than hoping the one before it is correct.
                        index = Array.BinarySearch(sourceMappingIndentationScopes, lineStart + 1);
                    }

                    if (index < 0)
                    {
                        // Couldn't find the exact value. Find the index of the element to the left of the searched value.
                        index = (~index) - 1;
                    }

                    if (index < 0)
                    {
                        // If we _still_ couldn't find the right indentation, then it probably means that the text is
                        // before the first source mapping location, so we can just place it in the minimum spot (realistically
                        // at index 0 in the razor file, but we use minCSharpIndentation because we're adjusting based on the
                        // generated file here)
                        csharpDesiredIndentation = minCSharpIndentation;
                    }
                    else
                    {
                        // index will now be set to the same value as the end of the closest source mapping.
                        var absoluteIndex = sourceMappingIndentationScopes[index];
                        csharpDesiredIndentation = sourceMappingIndentations[absoluteIndex];

                        // This means we didn't find an exact match and so we used the indentation of the end of a previous mapping.
                        // So let's use the MinCSharpIndentation of that same location if possible.
                        if (context.TryGetFormattingSpan(absoluteIndex, out var span))
                        {
                            minCSharpIndentation = context.GetIndentationOffsetForLevel(span.MinCSharpIndentLevel);
                        }
                    }
                }

                // Now let's use that information to figure out the effective C# indentation.
                // This should be based on context.
                // For instance, lines inside @code/@functions block should be reduced one level
                // and lines inside @{} should be reduced by two levels.

                if (csharpDesiredIndentation < minCSharpIndentation)
                {
                    // CSharp formatter doesn't want to indent this. Let's not touch it.
                    continue;
                }

                var effectiveCSharpDesiredIndentation = csharpDesiredIndentation - minCSharpIndentation;
                var razorDesiredIndentation = context.GetIndentationOffsetForLevel(indentations[i].IndentationLevel);
                if (indentations[i].StartsInHtmlContext)
                {
                    // This is a non-C# line.
                    if (context.IsFormatOnType)
                    {
                        // HTML formatter doesn't run in the case of format on type.
                        // Let's stick with our syntax understanding of HTML to figure out the desired indentation.
                    }
                    else
                    {
                        // Given that the HTML formatter ran before this, we can assume
                        // HTML is already correctly formatted. So we can use the existing indentation as is.
                        // We need to make sure to use the indentation size, as this will get passed to
                        // GetIndentationString eventually.
                        razorDesiredIndentation = indentations[i].ExistingIndentationSize;
                    }
                }

                var effectiveDesiredIndentation = razorDesiredIndentation + effectiveCSharpDesiredIndentation;

                // This will now contain the indentation we ultimately want to apply to this line.
                newIndentations[i] = effectiveDesiredIndentation;
            }

            // Now that we have collected all the indentations for each line, let's convert them to text edits.
            var changes = new List<TextChange>();
            foreach (var item in newIndentations)
            {
                var line = item.Key;
                var indentation = item.Value;
                Debug.Assert(indentation >= 0, "Negative indentation. This is unexpected.");

                var existingIndentationLength = indentations[line].ExistingIndentation;
                var spanToReplace = new TextSpan(context.SourceText.Lines[line].Start, existingIndentationLength);
                var effectiveDesiredIndentation = context.GetIndentationString(indentation);
                changes.Add(new TextChange(spanToReplace, effectiveDesiredIndentation));
            }

            return changes;
        }

        protected static bool ShouldFormat(FormattingContext context, TextSpan mappingSpan, bool allowImplicitStatements)
        {
            // We should be called with the range of various C# SourceMappings.

            if (mappingSpan.Start == 0)
            {
                // The mapping starts at 0. It can't be anything special but pure C#. Let's format it.
                return true;
            }

            var sourceText = context.SourceText;
            var absoluteIndex = mappingSpan.Start;

            if (mappingSpan.Length > 0)
            {
                // Slightly ugly hack to get around the behavior of LocateOwner.
                // In some cases, using the start of a mapping doesn't work well
                // because LocateOwner returns the previous node due to it owning the edge.
                // So, if we can try to find the owner using a position that fully belongs to the current mapping.
                absoluteIndex = mappingSpan.Start + 1;
            }

            var change = new SourceChange(absoluteIndex, 0, string.Empty);
            var syntaxTree = context.CodeDocument.GetSyntaxTree();
            var owner = syntaxTree.Root.LocateOwner(change);
            if (owner is null)
            {
                // Can't determine owner of this position. Optimistically allow formatting.
                return true;
            }

            owner = FixOwnerToWorkaroundCompilerQuirks(owner);

            // special case: If we're formatting implicit statements, we want to treat the `@attribute` directive as one
            // so that the C# definition of the attribute is formatted as C#
            if (allowImplicitStatements &&
                IsAttributeDirective())
            {
                return true;
            }

            if (IsRazorComment() ||
                IsInHtmlTag() ||
                IsInDirectiveWithNoKind() ||
                IsInSingleLineDirective() ||
                IsImplicitExpression() ||
                IsInSectionDirectiveCloseBrace() ||
                (!allowImplicitStatements && IsImplicitStatementStart()))
            {
                return false;
            }

            return true;

            bool IsRazorComment()
            {
                if (owner.IsCommentSpanKind())
                {
                    return true;
                }

                return false;
            }

            bool IsImplicitStatementStart()
            {
                // We will return true if the position points to the start of the C# portion of an implicit statement.
                // `@|for(...)` - true
                // `@|if(...)` - true
                // `@{|...` - false
                // `@code {|...` - false
                //

                if (owner.SpanStart == mappingSpan.Start &&
                    owner is CSharpStatementLiteralSyntax &&
                    owner.Parent is CSharpCodeBlockSyntax &&
                    owner.PreviousSpan() is CSharpTransitionSyntax)
                {
                    return true;
                }

                // Not an implicit statement.
                return false;
            }

            bool IsInHtmlTag()
            {
                // E.g, (| is position)
                //
                // `<p csharpattr="|Variable">` - true
                //
                return owner.AncestorsAndSelf().Any(
                    n => n is MarkupStartTagSyntax || n is MarkupTagHelperStartTagSyntax || n is MarkupEndTagSyntax || n is MarkupTagHelperEndTagSyntax);
            }

            bool IsInDirectiveWithNoKind()
            {
                // E.g, (| is position)
                //
                // `@using |System;
                //
                return owner.AncestorsAndSelf().Any(
                    n => n is RazorDirectiveSyntax { DirectiveDescriptor: null });
            }

            bool IsAttributeDirective()
            {
                // E.g, (| is position)
                //
                // `@attribute |[System.Obsolete]
                //
                return owner.AncestorsAndSelf().Any(
                    n => n is RazorDirectiveSyntax directive &&
                        directive.DirectiveDescriptor != null &&
                        directive.DirectiveDescriptor.Kind == DirectiveKind.SingleLine &&
                        directive.DirectiveDescriptor.Directive.Equals(AttributeDirective.Directive.Directive, StringComparison.Ordinal));
            }

            bool IsInSingleLineDirective()
            {
                // E.g, (| is position)
                //
                // `@inject |SomeType SomeName` - true
                //
                return owner.AncestorsAndSelf().Any(
                    n => n is RazorDirectiveSyntax directive && directive.DirectiveDescriptor.Kind == DirectiveKind.SingleLine);
            }

            bool IsImplicitExpression()
            {
                // E.g, (| is position)
                //
                // `@|foo` - true
                //
                return owner.AncestorsAndSelf().Any(n => n is CSharpImplicitExpressionSyntax);
            }

            bool IsInSectionDirectiveCloseBrace()
            {
                // @section Scripts {
                //     <script></script>
                // }
                //
                // We are fine to format these, but due to how they are generated (inside a multi-line lambda)
                // we want to exlude the final close brace from being formatted, or it will be indented by one
                // level due to the lambda. The rest we don't need to worry about, because the one level indent
                // is actually desirable.
                if (owner is MarkupTextLiteralSyntax &&
                    owner.Parent is MarkupBlockSyntax block &&
                    owner == block.Children[block.Children.Count - 1] &&
                    // MarkupBlock -> CSharpCodeBlock -> RazorDirectiveBody -> RazorDirective
                    block.Parent.Parent.Parent is RazorDirectiveSyntax directive &&
                    directive.DirectiveDescriptor.Directive == SectionDirective.Directive.Directive)
                {
                    return true;
                }

                return false;
            }
        }

        private static SyntaxNode FixOwnerToWorkaroundCompilerQuirks(SyntaxNode owner)
        {
            // Workaround for https://github.com/dotnet/aspnetcore/issues/36689
            // A tags owner comes back as itself if it is preceeded by a HTML comment,
            // because the whitespace between the comment and the tag is reported as not editable
            if (owner is MarkupTextLiteralSyntax &&
                owner.PreviousSpan() is MarkupTextLiteralSyntax literal &&
                literal.ContainsOnlyWhitespace() &&
                literal.PreviousSpan()?.Parent is MarkupCommentBlockSyntax)
            {
                owner = literal;
            }

            return owner;
        }
    }
}
