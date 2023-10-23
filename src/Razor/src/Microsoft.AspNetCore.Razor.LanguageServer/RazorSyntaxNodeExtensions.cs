// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Extensions;

internal static class RazorSyntaxNodeExtensions
{
    public static int GetLeadingWhitespaceLength(this SyntaxNode node, FormattingContext context)
    {
        var tokens = node.GetTokens();
        var whitespaceLength = 0;

        foreach (var token in tokens)
        {
            if (token.IsWhitespace())
            {
                if (token.Kind == SyntaxKind.NewLine)
                {
                    // We need to reset when we move to a new line.
                    whitespaceLength = 0;
                }
                else if (token.IsSpace())
                {
                    whitespaceLength++;
                }
                else if (token.IsTab())
                {
                    whitespaceLength += (int)context.Options.TabSize;
                }
            }
            else
            {
                break;
            }
        }

        return whitespaceLength;
    }

    public static int GetTrailingWhitespaceLength(this SyntaxNode node, FormattingContext context)
    {
        var tokens = node.GetTokens();
        var whitespaceLength = 0;

        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            var token = tokens[i];
            if (token.IsWhitespace())
            {
                if (token.Kind == SyntaxKind.NewLine)
                {
                    whitespaceLength = 0;
                }
                else if (token.IsSpace())
                {
                    whitespaceLength++;
                }
                else if (token.IsTab())
                {
                    whitespaceLength += (int)context.Options.TabSize;
                }
            }
            else
            {
                break;
            }
        }

        return whitespaceLength;
    }
}
