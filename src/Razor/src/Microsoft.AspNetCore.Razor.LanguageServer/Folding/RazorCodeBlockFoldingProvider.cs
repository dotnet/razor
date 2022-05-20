// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Folding
{
    internal class RazorCodeBlockFoldingProvider : RazorFoldingRangeProvider
    {
        public override async Task<ImmutableArray<FoldingRange>> GetFoldingRangesAsync(RazorCodeDocument codeDocument, DocumentSnapshot documentSnapshot, CancellationToken cancellationToken)
        {
            var sourceText = await documentSnapshot.GetTextAsync().ConfigureAwait(false);

            var syntaxTree = codeDocument.GetSyntaxTree();
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
                };

                builder.Add(foldingRange);
            }

            return builder.ToImmutableArray();
        }
    }
}
