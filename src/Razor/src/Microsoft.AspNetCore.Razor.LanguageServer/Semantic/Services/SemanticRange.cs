// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class SemanticRange
    {
        public SemanticRange(SyntaxNode node, SyntaxKind kind, RazorCodeDocument razorCodeDocument)
        {
            var range = node.GetRange(razorCodeDocument.Source);
            Range = range;
            Kind = GetTokenTypeData(kind);
        }

        public Range Range { get; set; }

        public uint Kind { get; set; }

        private static uint GetTokenTypeData(SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.MarkupTagHelperDirectiveAttribute:
                case SyntaxKind.MarkupMinimizedTagHelperDirectiveAttribute:
                    return SemanticTokensLegend.TokenTypesLegend[SemanticTokensLegend.RazorDirectiveAttribute];
                case SyntaxKind.MarkupTagHelperStartTag:
                case SyntaxKind.MarkupTagHelperEndTag:
                    return SemanticTokensLegend.TokenTypesLegend[SemanticTokensLegend.RazorTagHelperElement];
                case SyntaxKind.MarkupTagHelperAttribute:
                case SyntaxKind.MarkupMinimizedTagHelperAttribute:
                    return SemanticTokensLegend.TokenTypesLegend[SemanticTokensLegend.RazorTagHelperAttribute];
                case SyntaxKind.Transition:
                    return SemanticTokensLegend.TokenTypesLegend[SemanticTokensLegend.RazorTransition];
                case SyntaxKind.Colon:
                    return SemanticTokensLegend.TokenTypesLegend[SemanticTokensLegend.RazorDirectiveColon];
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
