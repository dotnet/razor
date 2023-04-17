// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal static class SyntaxNodeUtils
{
    internal static bool IsUsingDirective(SyntaxNode node, [NotNullWhen(true)] out SyntaxList<SyntaxNode>? children)
    {
        // Using directives are weird, because the directive keyword ("using") is part of the C# statement it represents
        if (node is RazorDirectiveSyntax razorDirective &&
            razorDirective.DirectiveDescriptor is null &&
            razorDirective.Body is RazorDirectiveBodySyntax body &&
            body.Keyword is CSharpStatementLiteralSyntax literal &&
            literal.LiteralTokens.Count > 0)
        {
            if (literal.LiteralTokens[0] is { Kind: SyntaxKind.Keyword, Content: "using" })
            {
                children = literal.LiteralTokens;
                return true;
            }
        }

        children = null;
        return false;
    }
}
