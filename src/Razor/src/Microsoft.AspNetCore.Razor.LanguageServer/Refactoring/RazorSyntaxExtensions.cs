using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class RazorSyntaxExtensions
    {

        private static readonly SyntaxKind[] RelevantSpanKinds = new SyntaxKind[]
        {
            SyntaxKind.RazorMetaCode,
            SyntaxKind.CSharpTransition,
            SyntaxKind.MarkupTransition,
            SyntaxKind.CSharpStatementLiteral,
            SyntaxKind.CSharpExpressionLiteral,
            SyntaxKind.CSharpEphemeralTextLiteral,
            SyntaxKind.MarkupTextLiteral,
            SyntaxKind.MarkupEphemeralTextLiteral,
            SyntaxKind.UnclassifiedTextLiteral,
        };

        public static SyntaxNode GetNodeFromLocation(SyntaxNode node, SourceLocation location)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            // Shortcircuit if we're out of the possible bounds
            if (location.AbsoluteIndex < node.Position || node.EndPosition < location.AbsoluteIndex)
            {
                
                return null;
            }

            if (RelevantSpanKinds.Contains(node.Kind))
            {
                if (node.Position <= location.AbsoluteIndex && location.AbsoluteIndex < node.EndPosition)
                {
                    return node;
                }
            }

            IReadOnlyList<SyntaxNode> children;
            if (node is MarkupStartTagSyntax startTag)
            {
                children = startTag.Children;
            }
            else if (node is MarkupEndTagSyntax endTag)
            {
                children = endTag.Children;
            }
            else if (node is MarkupTagHelperStartTagSyntax startTagHelper)
            {
                children = startTagHelper.Children;
            }
            else if (node is MarkupTagHelperEndTagSyntax endTagHelper)
            {
                children = endTagHelper.Children;
            }
            else
            {
                children = node.ChildNodes();
            }

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                node = GetNodeFromLocation(child, location);
                if (node != null)
                {
                    break;
                }
            }

            return node;
        }
    }
}
