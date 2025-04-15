﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.FoldingRanges;

internal abstract class AbstractSyntaxNodeFoldingProvider<TNode> : IRazorFoldingRangeProvider
    where TNode : RazorSyntaxNode
{
    public ImmutableArray<FoldingRange> GetFoldingRanges(RazorCodeDocument codeDocument)
    {
        var sourceText = codeDocument.Source.Text;
        var syntaxTree = codeDocument.GetSyntaxTree();
        var nodes = GetFoldableNodes(syntaxTree);

        using var builder = new PooledArrayBuilder<FoldingRange>(nodes.Length);
        foreach (var node in nodes)
        {
            var (start, end) = sourceText.GetLinePositionSpan(node.Span);
            var foldingRange = new FoldingRange()
            {
                StartCharacter = start.Character,
                StartLine = start.Line,
                EndCharacter = end.Character,
                EndLine = end.Line,

                // Directives remove the "@" but for collapsing we want to keep it for users.
                // Shows "@code" instead of "code".
                CollapsedText = GetCollapsedText(node)
            };

            builder.Add(foldingRange);
        }

        return builder.DrainToImmutable();
    }

    protected abstract ImmutableArray<TNode> GetFoldableNodes(RazorSyntaxTree syntaxTree);

    protected abstract string GetCollapsedText(TNode node);
}
