// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.FoldingRanges;

internal class UsingsFoldingRangeProvider : IRazorFoldingRangeProvider
{
    public ImmutableArray<FoldingRange> GetFoldingRanges(RazorCodeDocument codeDocument)
    {
        using var ranges = new PooledArrayBuilder<FoldingRange>();

        var sourceDocument = codeDocument.Source;
        var root = codeDocument.GetRequiredSyntaxRoot();

        foreach (var node in root.DescendantNodes(static n => n.MayContainDirectives()))
        {
            if (node is not RazorDirectiveSyntax directive || !directive.IsUsingDirective(out _))
            {
                continue;
            }

            // first rule: we can fold consecutive usings if we have an existing range that
            // ends the line *before* this current using directive or begins the line *after*,
            // we extend the range one line

            var span = directive.GetLinePositionSpan(sourceDocument);

            if (ranges.Count > 0 && ranges[^1] is { } lastRange)
            {
                if (lastRange.EndLine + 1 == span.Start.Line)
                {
                    lastRange.EndLine = span.End.Line;
                    lastRange.EndCharacter = span.End.Character;
                    continue;
                }
                else if (lastRange.StartLine - 1 == span.End.Line)
                {
                    lastRange.StartLine = span.Start.Line;
                    lastRange.StartCharacter = span.Start.Character;
                    continue;
                }
            }

            ranges.Add(LspFactory.CreateFoldingRange(FoldingRangeKind.Imports, span));
        }

        return ranges.ToImmutableAndClear();
    }
}
