// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language.Syntax;

internal partial class MarkupTagHelperEndTagSyntax : IEndTagSyntaxNode
{
    private SyntaxNode? _lazyChildren;

    // Copied directly from MarkupEndTagSyntax Children & GetLegacyChildren.

    public SyntaxList<RazorSyntaxNode> LegacyChildren
    {
        get
        {
            var children = _lazyChildren ??
                InterlockedOperations.Initialize(ref _lazyChildren, this.ComputeEndTagLegacyChildren());

            return new SyntaxList<RazorSyntaxNode>(children);
        }
    }
}
