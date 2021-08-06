// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public static class SyntaxListExtensions
    {
        internal static SyntaxNode PreviousSiblingOrSelf(this SyntaxList<RazorSyntaxNode> syntaxList, RazorSyntaxNode syntaxNode)
        {
            var index = syntaxList.IndexOf(syntaxNode);

            if (index == 0)
            {
                return syntaxNode;
            }
            else
            {
                return syntaxList[index - 1];
            }
        }

        internal static SyntaxNode NextSiblingOrSelf(this SyntaxList<RazorSyntaxNode> syntaxList, RazorSyntaxNode syntaxNode)
        {
            var index = syntaxList.IndexOf(syntaxNode);

            if (index == syntaxList.Count - 1)
            {
                return syntaxNode;
            }
            else
            {
                return syntaxList[index + 1];
            }
        }
    }
}
