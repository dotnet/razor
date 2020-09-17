// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    internal class RazorAndCSharpSemanticTokensLegend
    {
        private static string[] tokenModifiers(SemanticTokensLegend csharpTokensLegend) {
            var razorTokenModifiers = RazorSemanticTokensLegend.TokenModifiers.ToArray();

            return razorTokenModifiers;
        }

        private static string[] tokenTypes(SemanticTokensLegend csharpTokensLegend) {
            var razorTokenTypes = RazorSemanticTokensLegend.TokenTypes.ToArray();

            return razorTokenTypes;
        }

        public static SemanticTokensLegend Instance(SemanticTokensLegend csharpTokensLegend)
        {
            return new SemanticTokensLegend
            {
                TokenModifiers = tokenModifiers(csharpTokensLegend),
                TokenTypes = tokenTypes(csharpTokensLegend),
            };

        }
    }
}
