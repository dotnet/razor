// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models
{
    internal class RazorSemanticTokensLegend
    {
        private static readonly SemanticTokenType RazorComponentElementType = new SemanticTokenType("razorComponentElement");
        private static readonly SemanticTokenType RazorComponentAttributeType = new SemanticTokenType("RazorComponentAttribute");
        private static readonly SemanticTokenType RazorTagHelperElementType = new SemanticTokenType("razorTagHelperElement");
        private static readonly SemanticTokenType RazorTagHelperAttributeType = new SemanticTokenType("razorTagHelperAttribute");
        private static readonly SemanticTokenType RazorTransitionType = new SemanticTokenType("razorTransition");
        private static readonly SemanticTokenType RazorDirectiveAttributeType = new SemanticTokenType("razorDirectiveAttribute");
        private static readonly SemanticTokenType RazorDirectiveColonType = new SemanticTokenType("razorDirectiveColon");
        private static readonly SemanticTokenType RazorDirectiveType = new SemanticTokenType("razorDirective");
        private static readonly SemanticTokenType RazorCommentType = new SemanticTokenType("razorComment");
        private static readonly SemanticTokenType RazorCommentTransitionType = new SemanticTokenType("razorCommentTransition");
        private static readonly SemanticTokenType RazorCommentStarType = new SemanticTokenType("razorCommentStar");

        private static readonly SemanticTokenType MarkupTagDelimiterType = new SemanticTokenType("markupTagDelimiter");
        private static readonly SemanticTokenType MarkupOperatorType = new SemanticTokenType("markupOperator");
        private static readonly SemanticTokenType MarkupElementType = new SemanticTokenType("markupElement");
        private static readonly SemanticTokenType MarkupAttributeType = new SemanticTokenType("markupAttribute");
        private static readonly SemanticTokenType MarkupAttributeValueType = new SemanticTokenType("markupAttributeValue");
        private static readonly SemanticTokenType MarkupAttributeQuoteType = new SemanticTokenType("markupAttributeQuote");
        private static readonly SemanticTokenType MarkupTextLiteralType = new SemanticTokenType("markupTextLiteral");
        private static readonly SemanticTokenType MarkupCommentPunctuationType = new SemanticTokenType("markupCommentPunctuation");
        private static readonly SemanticTokenType MarkupCommentType = new SemanticTokenType("markupComment");

        public static int RazorCommentTransition => TokenTypesLegend[RazorCommentTransitionType];
        public static int RazorCommentStar => TokenTypesLegend[RazorCommentStarType];
        public static int RazorComment => TokenTypesLegend[RazorCommentType];
        public static int RazorTransition => TokenTypesLegend[RazorTransitionType];
        public static int RazorComponentElement => TokenTypesLegend[RazorComponentElementType];
        public static int RazorComponentAttribute => TokenTypesLegend[RazorComponentAttributeType];
        public static int RazorTagHelperElement => TokenTypesLegend[RazorTagHelperElementType];
        public static int RazorTagHelperAttribute => TokenTypesLegend[RazorTagHelperAttributeType];
        public static int MarkupTagDelimiter => TokenTypesLegend[MarkupTagDelimiterType];
        public static int MarkupOperator => TokenTypesLegend[MarkupOperatorType];
        public static int MarkupElement => TokenTypesLegend[MarkupElementType];
        public static int MarkupAttribute => TokenTypesLegend[MarkupAttributeType];
        public static int MarkupAttributeValue => TokenTypesLegend[MarkupAttributeValueType];
        public static int MarkupAttributeQuote => TokenTypesLegend[MarkupAttributeQuoteType];
        public static int RazorDirectiveAttribute => TokenTypesLegend[RazorDirectiveAttributeType];
        public static int RazorDirectiveColon => TokenTypesLegend[RazorDirectiveColonType];
        public static int RazorDirective => TokenTypesLegend[RazorDirectiveType];
        public static int MarkupTextLiteral => TokenTypesLegend[MarkupTextLiteralType];
        public static int MarkupCommentPunctuation => TokenTypesLegend[MarkupCommentPunctuationType];
        public static int MarkupComment => TokenTypesLegend[MarkupCommentType];

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
            new SemanticTokenType("json - array"),
            new SemanticTokenType("json - comment"),
            new SemanticTokenType("json - constructor name"),
            new SemanticTokenType("json - keyword"),
            new SemanticTokenType("json - number"),
            new SemanticTokenType("json - object"),
            new SemanticTokenType("json - operator"),
            new SemanticTokenType("json - property name"), // 40
            new SemanticTokenType("json - punctuation"),
            new SemanticTokenType("json - string"),
            new SemanticTokenType("json - text"),
            new SemanticTokenType("label name"),
            new SemanticTokenType("local name"),
            new SemanticTokenType("method name"),
            new SemanticTokenType("module name"),
            new SemanticTokenType("namespace name"),
            new SemanticTokenType("operator - overloaded"),
            new SemanticTokenType("parameter name"), // 50
            new SemanticTokenType("property name"),
            new SemanticTokenType("preprocessor keyword"),
            new SemanticTokenType("preprocessor text"),
            new SemanticTokenType("punctuation"),
            new SemanticTokenType("record class name"),
            new SemanticTokenType("record struct name"),
            new SemanticTokenType("regex - alternation"),
            new SemanticTokenType("regex - anchor"),
            new SemanticTokenType("regex - character class"),
            new SemanticTokenType("regex - comment"), // 60
            new SemanticTokenType("regex - grouping"),
            new SemanticTokenType("regex - other escape"),
            new SemanticTokenType("regex - quantifier"),
            new SemanticTokenType("regex - self escaped character"),
            new SemanticTokenType("regex - text"),
            new SemanticTokenType("string - escape character"),
            new SemanticTokenType("struct name"),
            new SemanticTokenType("text"),
            new SemanticTokenType("type parameter name"),
            new SemanticTokenType("string - verbatim"), // 70
            new SemanticTokenType("whitespace"),
            new SemanticTokenType("xml doc comment - attribute name"),
            new SemanticTokenType("xml doc comment - attribute quotes"),
            new SemanticTokenType("xml doc comment - attribute value"),
            new SemanticTokenType("xml doc comment - cdata section"),
            new SemanticTokenType("xml doc comment - comment"),
            new SemanticTokenType("xml doc comment - delimiter"),
            new SemanticTokenType("xml doc comment - entity reference"),
            new SemanticTokenType("xml doc comment - name"),
            new SemanticTokenType("xml doc comment - processing instruction"), // 80
            new SemanticTokenType("xml doc comment - text"),
            new SemanticTokenType("xml literal - attribute name"),
            new SemanticTokenType("xml literal - attribute quotes"),
            new SemanticTokenType("xml literal - attribute value"),
            new SemanticTokenType("xml literal - cdata section"),
            new SemanticTokenType("xml literal - comment"),
            new SemanticTokenType("xml literal - delimiter"),
            new SemanticTokenType("xml literal - embedded expression"),
            new SemanticTokenType("xml literal - entity reference"),
            new SemanticTokenType("xml literal - name"), // 90
            new SemanticTokenType("xml literal - processing instruction"),
            new SemanticTokenType("xml literal - text"),
            RazorTagHelperElementType,
            RazorTagHelperAttributeType,
            RazorTransitionType,
            RazorDirectiveColonType,
            RazorDirectiveAttributeType,
            RazorDirectiveType,
            RazorCommentType,
            RazorCommentTransitionType, // 100
            RazorCommentStarType,
            MarkupTagDelimiterType,
            MarkupElementType,
            MarkupOperatorType,
            MarkupAttributeType,
            MarkupAttributeQuoteType,
            MarkupTextLiteralType,
            MarkupCommentPunctuationType,
            MarkupCommentType,
            MarkupAttributeValueType, // 110
            RazorComponentElementType,
            RazorComponentAttributeType,
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
