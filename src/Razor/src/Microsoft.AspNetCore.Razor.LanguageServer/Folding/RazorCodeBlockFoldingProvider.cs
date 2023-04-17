// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Folding;

internal sealed class RazorCodeBlockFoldingProvider : IRazorFoldingRangeProvider
{
    public async Task<ImmutableArray<FoldingRange>> GetFoldingRangesAsync(DocumentContext documentContext, CancellationToken cancellationToken)
    {
        var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        var syntaxTree = await documentContext.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        var codeBlocks = syntaxTree.GetCodeBlockDirectives();

        var builder = new List<FoldingRange>();
        foreach (var codeBlock in codeBlocks)
        {
            sourceText.GetLineAndOffset(codeBlock.Span.Start, out var startLine, out var startOffset);
            sourceText.GetLineAndOffset(codeBlock.Span.End, out var endLine, out var endOffset);
            var foldingRange = new FoldingRange()
            {
                StartCharacter = startOffset,
                StartLine = startLine,
                EndCharacter = endOffset,
                EndLine = endLine,

                // Directives remove the "@" but for collapsing we want to keep it for users.
                // Shows "@code" instead of "code".
                CollapsedText = "@" + codeBlock.DirectiveDescriptor.Directive
            };

            builder.Add(foldingRange);
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        var razorFileSyntaxWalker = new RazorFileFoldingRangeSyntaxWalker(codeDocument.Source);
        razorFileSyntaxWalker.Visit(codeDocument.GetSyntaxTree().Root);
        builder.AddRange(razorFileSyntaxWalker.Ranges);

        return builder.ToImmutableArray();
    }

    private class RazorFileFoldingRangeSyntaxWalker : SyntaxWalker
    {
        private readonly RazorSourceDocument _source;
        internal List<FoldingRange> Ranges { get; }

        public RazorFileFoldingRangeSyntaxWalker(RazorSourceDocument source)
        {
            _source = source;
            Ranges = new List<FoldingRange>();
        }

        public override void VisitRazorDirective(RazorDirectiveSyntax node)
        {
            // first rule: we can fold consecutive usings
            // if we have an existing range that ends the line *before* this current using directive or begins the line *after*,
            // we extend the range one line
            if (SyntaxNodeUtils.IsUsingDirective(node, out _))
            {
                var linePosition = node.GetLinePositionSpan(_source);

                var isPartOfExistingRange = false;
                if (Ranges.LastOrDefault() is { } lastRange)
                {
                    if (lastRange.EndLine + 1 == linePosition.Start.Line)
                    {
                        lastRange.EndLine = linePosition.End.Line;
                        lastRange.EndCharacter = linePosition.End.Character;
                        isPartOfExistingRange = true;
                    }
                    else if (lastRange.StartLine - 1 == linePosition.End.Line)
                    {
                        lastRange.StartLine = linePosition.Start.Line;
                        lastRange.StartCharacter = linePosition.Start.Character;
                        isPartOfExistingRange = true;
                    }
                }


                if (!isPartOfExistingRange)
                {
                    Ranges.Add(new FoldingRange
                    {
                        StartLine = linePosition.Start.Line,
                        StartCharacter = linePosition.Start.Character,
                        EndLine = linePosition.End.Line,
                        EndCharacter = linePosition.End.Character,
                        Kind = FoldingRangeKind.Imports
                    });
                }
            }
        }
    }
}
