// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models
{
    internal class RazorSemanticTokensLegend
    {
        private static readonly string RazorComponentElementType = "razorComponentElement";
        private static readonly string RazorComponentAttributeType = "RazorComponentAttribute";
        private static readonly string RazorTagHelperElementType = "razorTagHelperElement";
        private static readonly string RazorTagHelperAttributeType = "razorTagHelperAttribute";
        private static readonly string RazorTransitionType = "razorTransition";
        private static readonly string RazorDirectiveAttributeType = "razorDirectiveAttribute";
        private static readonly string RazorDirectiveColonType = "razorDirectiveColon";
        private static readonly string RazorDirectiveType = "razorDirective";
        private static readonly string RazorCommentType = "razorComment";
        private static readonly string RazorCommentTransitionType = "razorCommentTransition";
        private static readonly string RazorCommentStarType = "razorCommentStar";

        private static readonly string MarkupTagDelimiterType = "markupTagDelimiter";
        private static readonly string MarkupOperatorType = "markupOperator";
        private static readonly string MarkupElementType = "markupElement";
        private static readonly string MarkupAttributeType = "markupAttribute";
        private static readonly string MarkupAttributeValueType = "markupAttributeValue";
        private static readonly string MarkupAttributeQuoteType = "markupAttributeQuote";
        private static readonly string MarkupTextLiteralType = "markupTextLiteral";
        private static readonly string MarkupCommentPunctuationType = "markupCommentPunctuation";
        private static readonly string MarkupCommentType = "markupComment";

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

        public static readonly string[] TokenTypes = new string[] {
            // C# token types
            "namespace", // 0
            "type",
            "class",
            "enum",
            "interface",
            "struct",
            "typeParameter",
            "parameter",
            "variable",
            "property",
            "enumMember", // 10
            "event",
            "function",
            "member",
            "macro",
            "keyword",
            "modifier",
            "comment",
            "string",
            "number",
            "regexp", // 20
            "operator",
            "class name",
            "constant name",
            "keyword - control",
            "delegate name",
            "enum member name",
            "enum name",
            "event name",
            "excluded code",
            "extension method name", // 30
            "field name",
            "interface name",
            "json - array",
            "json - comment"),
            "json - constructor name",
            "json - keyword",
            "json - number",
            "json - object",
            "json - operator",
            "json - property name", // 40
            "json - punctuation",
            "json - string",
            "json - text",
            "label name",
            "local name",
            "method name",
            "module name",
            "namespace name",
            "operator - overloaded",
            "parameter name", // 50
            "property name",
            "preprocessor keyword",
            "preprocessor text",
            "punctuation",
            "record class name",
            "record struct name",
            "regex - alternation",
            "regex - anchor",
            "regex - character class",
            "regex - comment", // 60
            "regex - grouping",
            "regex - other escape",
            "regex - quantifier",
            "regex - self escaped character",
            "regex - text",
            "string - escape character",
            "struct name",
            "text",
            "type parameter name",
            "string - verbatim", // 70
            "whitespace",
            "xml doc comment - attribute name",
            "xml doc comment - attribute quotes",
            "xml doc comment - attribute value",
            "xml doc comment - cdata section",
            "xml doc comment - comment",
            "xml doc comment - delimiter",
            "xml doc comment - entity reference",
            "xml doc comment - name",
            "xml doc comment - processing instruction", // 80
            "xml doc comment - text",
            "xml literal - attribute name",
            "xml literal - attribute quotes",
            "xml literal - attribute value",
            "xml literal - cdata section",
            "xml literal - comment",
            "xml literal - delimiter",
            "xml literal - embedded expression",
            "xml literal - entity reference",
            "xml literal - name", // 90
            "xml literal - processing instruction",
            "xml literal - text",
            RazorTagHelperElementType,
            RazorTagHelperAttributeType,
            RazorTransitionType,
            RazorDirectiveColonType,
            RazorDirectiveAttributeType,
            RazorDirectiveType,
            RazorCommentType, // 100
            RazorCommentTransitionType,
            RazorCommentStarType,
            MarkupTagDelimiterType,
            MarkupElementType,
            MarkupOperatorType,
            MarkupAttributeType,
            MarkupAttributeQuoteType,
            MarkupTextLiteralType,
            MarkupCommentPunctuationType,
            MarkupCommentType, // 110
            MarkupAttributeValueType,
            RazorComponentElementType,
            RazorComponentAttributeType,
        };

        private static readonly string[] s_tokenModifiers = new string[] {
            // Razor
            "None",
            // C# Modifiers
            "static",
        };

        public static readonly IReadOnlyDictionary<string, int> TokenTypesLegend = GetMap(TokenTypes);

        public static readonly SemanticTokensLegend Instance = new SemanticTokensLegend
        {
            TokenModifiers = s_tokenModifiers,
            TokenTypes = TokenTypes,
        };

        private static IReadOnlyDictionary<string, int> GetMap(IReadOnlyList<string> tokens)
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
