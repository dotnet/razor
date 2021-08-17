// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models
{
    internal class RazorSemanticTokensLegend
    {
        private static readonly SemanticTokenType RazorComponentElementString = new SemanticTokenType("razorComponentElement");
        private static readonly SemanticTokenType RazorComponentAttributeString = new SemanticTokenType("RazorComponentAttribute");
        private static readonly SemanticTokenType RazorTagHelperElementString = new SemanticTokenType("razorTagHelperElement");
        private static readonly SemanticTokenType RazorTagHelperAttributeString = new SemanticTokenType("razorTagHelperAttribute");
        private static readonly SemanticTokenType RazorTransitionString = new SemanticTokenType("razorTransition");
        private static readonly SemanticTokenType RazorDirectiveAttributeString = new SemanticTokenType("razorDirectiveAttribute");
        private static readonly SemanticTokenType RazorDirectiveColonString = new SemanticTokenType("razorDirectiveColon");
        private static readonly SemanticTokenType RazorDirectiveString = new SemanticTokenType("razorDirective");
        private static readonly SemanticTokenType RazorCommentString = new SemanticTokenType("razorComment");
        private static readonly SemanticTokenType RazorCommentTransitionString = new SemanticTokenType("razorCommentTransition");
        private static readonly SemanticTokenType RazorCommentStarString = new SemanticTokenType("razorCommentStar");

        private static readonly SemanticTokenType MarkupTagDelimiterString = new SemanticTokenType("markupTagDelimiter");
        private static readonly SemanticTokenType MarkupOperatorString = new SemanticTokenType("markupOperator");
        private static readonly SemanticTokenType MarkupElementString = new SemanticTokenType("markupElement");
        private static readonly SemanticTokenType MarkupAttributeString = new SemanticTokenType("markupAttribute");
        private static readonly SemanticTokenType MarkupAttributeValueString = new SemanticTokenType("markupAttributeValue");
        private static readonly SemanticTokenType MarkupAttributeQuoteString = new SemanticTokenType("markupAttributeQuote");
        private static readonly SemanticTokenType MarkupTextLiteralString = new SemanticTokenType("markupTextLiteral");
        private static readonly SemanticTokenType MarkupCommentPunctuationString = new SemanticTokenType("markupCommentPunctuation");
        private static readonly SemanticTokenType MarkupCommentString = new SemanticTokenType("markupComment");

        public static int RazorCommentTransition => TokenTypesLegend[RazorCommentTransitionString];
        public static int RazorCommentStar => TokenTypesLegend[RazorCommentStarString];
        public static int RazorComment => TokenTypesLegend[RazorCommentString];
        public static int RazorTransition => TokenTypesLegend[RazorTransitionString];
        public static int RazorComponentElement => TokenTypesLegend[RazorComponentElementString];
        public static int RazorComponentAttribute => TokenTypesLegend[RazorComponentAttributeString];
        public static int RazorTagHelperElement => TokenTypesLegend[RazorTagHelperElementString];
        public static int RazorTagHelperAttribute => TokenTypesLegend[RazorTagHelperAttributeString];
        public static int MarkupTagDelimiter => TokenTypesLegend[MarkupTagDelimiterString];
        public static int MarkupOperator => TokenTypesLegend[MarkupOperatorString];
        public static int MarkupElement => TokenTypesLegend[MarkupElementString];
        public static int MarkupAttribute => TokenTypesLegend[MarkupAttributeString];
        public static int MarkupAttributeValue => TokenTypesLegend[MarkupAttributeValueString];
        public static int MarkupAttributeQuote => TokenTypesLegend[MarkupAttributeQuoteString];
        public static int RazorDirectiveAttribute => TokenTypesLegend[RazorDirectiveAttributeString];
        public static int RazorDirectiveColon => TokenTypesLegend[RazorDirectiveColonString];
        public static int RazorDirective => TokenTypesLegend[RazorDirectiveString];
        public static int MarkupTextLiteral => TokenTypesLegend[MarkupTextLiteralString];
        public static int MarkupCommentPunctuation => TokenTypesLegend[MarkupCommentPunctuationString];
        public static int MarkupComment => TokenTypesLegend[MarkupCommentString];

        public static int CSharpKeyword => TokenTypesLegend["keyword"];
        public static int CSharpVariable => TokenTypesLegend["variable"];
        public static int CSharpOperator => TokenTypesLegend["operator"];
        public static int CSharpString => TokenTypesLegend["string"];
        public static int CSharpPunctuation => TokenTypesLegend["punctuation"];

        public static readonly IReadOnlyList<SemanticTokenType> TokenTypes = new SemanticTokenType[] {
            // C# token types
            new SemanticTokenType("namespace"), // 0
            new SemanticTokenType("type"),
            new SemanticTokenType("class"),
            new SemanticTokenType("enum"),
            new SemanticTokenType("interface"),
            new SemanticTokenType("struct"),
            new SemanticTokenType("typeParameter"),
            new SemanticTokenType("parameter"),
            new SemanticTokenType("variable"),
            new SemanticTokenType("property"),
            new SemanticTokenType("enumMember"), // 10
            new SemanticTokenType("event"),
            new SemanticTokenType("function"),
            new SemanticTokenType("member"),
            new SemanticTokenType("macro"),
            new SemanticTokenType("keyword"),
            new SemanticTokenType("modifier"),
            new SemanticTokenType("comment"),
            new SemanticTokenType("string"),
            new SemanticTokenType("number"),
            new SemanticTokenType("regexp"), // 20
            new SemanticTokenType("operator"),
            new SemanticTokenType("class name"),
            new SemanticTokenType("constant name"),
            new SemanticTokenType("keyword - control"),
            new SemanticTokenType("delegate name"),
            new SemanticTokenType("enum member name"),
            new SemanticTokenType("enum name"),
            new SemanticTokenType("event name"),
            new SemanticTokenType("excluded code"),
            new SemanticTokenType("extension method name"), // 30
            new SemanticTokenType("field name"),
            new SemanticTokenType("interface name"),
            new SemanticTokenType("label name"),
            new SemanticTokenType("local name"),
            new SemanticTokenType("method name"),
            new SemanticTokenType("module name"),
            new SemanticTokenType("namespace name"),
            new SemanticTokenType("operator - overloaded"),
            new SemanticTokenType("parameter name"),
            new SemanticTokenType("property name"), // 40
            new SemanticTokenType("preprocessor keyword"),
            new SemanticTokenType("preprocessor text"),
            new SemanticTokenType("punctuation"),
            new SemanticTokenType("record class name"),
            new SemanticTokenType("record struct name"),
            new SemanticTokenType("regex - alternation"),
            new SemanticTokenType("regex - anchor"),
            new SemanticTokenType("regex - character class"),
            new SemanticTokenType("regex - comment"),
            new SemanticTokenType("regex - grouping"), // 50
            new SemanticTokenType("regex - other escape"),
            new SemanticTokenType("regex - quantifier"),
            new SemanticTokenType("regex - self escaped character"),
            new SemanticTokenType("regex - text"),
            new SemanticTokenType("string - escape character"),
            new SemanticTokenType("struct name"),
            new SemanticTokenType("text"),
            new SemanticTokenType("type parameter name"),
            new SemanticTokenType("string - verbatim"),
            new SemanticTokenType("whitespace"), // 60
            new SemanticTokenType("xml doc comment - attribute name"),
            new SemanticTokenType("xml doc comment - attribute quotes"),
            new SemanticTokenType("xml doc comment - attribute value"),
            new SemanticTokenType("xml doc comment - cdata section"),
            new SemanticTokenType("xml doc comment - comment"),
            new SemanticTokenType("xml doc comment - delimiter"),
            new SemanticTokenType("xml doc comment - entity reference"),
            new SemanticTokenType("xml doc comment - name"),
            new SemanticTokenType("xml doc comment - processing instruction"),
            new SemanticTokenType("xml doc comment - text"), // 70
            new SemanticTokenType("xml literal - attribute name"),
            new SemanticTokenType("xml literal - attribute quotes"),
            new SemanticTokenType("xml literal - attribute value"),
            new SemanticTokenType("xml literal - cdata section"),
            new SemanticTokenType("xml literal - comment"),
            new SemanticTokenType("xml literal - delimiter"),
            new SemanticTokenType("xml literal - embedded expression"),
            new SemanticTokenType("xml literal - entity reference"),
            new SemanticTokenType("xml literal - name"),
            new SemanticTokenType("xml literal - processing instruction"), // 80
            new SemanticTokenType("xml literal - text"),
            RazorTagHelperElementString,
            RazorTagHelperAttributeString,
            RazorTransitionString,
            RazorDirectiveColonString,
            RazorDirectiveAttributeString,
            RazorDirectiveString,
            RazorCommentString,
            RazorCommentTransitionString,
            RazorCommentStarString, // 90
            MarkupTagDelimiterString,
            MarkupElementString,
            MarkupOperatorString,
            MarkupAttributeString,
            MarkupAttributeQuoteString,
            MarkupTextLiteralString,
            MarkupCommentPunctuationString,
            MarkupCommentString,
            MarkupAttributeValueString,
            RazorComponentElementString, // 100
            RazorComponentAttributeString,
        };

        private static readonly IReadOnlyList<SemanticTokenModifier> s_tokenModifiers = new SemanticTokenModifier[] {
            // Razor
            new SemanticTokenModifier("None"),
            // C# Modifiers
            new SemanticTokenModifier("static"),
        };

        public static readonly IReadOnlyDictionary<string, int> TokenTypesLegend = GetMap(TokenTypes);

        public static readonly SemanticTokensLegend Instance = new SemanticTokensLegend
        {
            TokenModifiers = new Container<SemanticTokenModifier>(s_tokenModifiers),
            TokenTypes = new Container<SemanticTokenType>(TokenTypes),
        };

        private static IReadOnlyDictionary<string, int> GetMap(IReadOnlyList<SemanticTokenType> tokens)
        {
            var result = new Dictionary<string, int>();
            for (var i = 0; i < tokens.Count; i++)
            {
                result[tokens[i]] = i;
            }

            return result;
        }
    }
}
