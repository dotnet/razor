using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    static class RazorSyntaxExtensions
    {
        /// <summary>
        /// Search for the deepest descendent whose span contains the location and satisfying the filter, if provided.
        /// Will null if such a node is not found.
        /// </summary>
        /// <param name="codeDocument">This code document.</param>
        /// <param name="location">The location to search for.</param>
        /// <param name="filter">SyntaxNode constraint.</param>
        /// <returns>A node at the given location or null.</returns>
        public static SyntaxNode GetNodeAtLocation(this RazorCodeDocument codeDocument, SourceLocation location, Func<SyntaxNode, bool> filter = null)
        {
            return GetNodeAtLocation(codeDocument.GetSyntaxTree().Root, location, filter);
        }

        private static SyntaxNode GetNodeAtLocation(SyntaxNode node, SourceLocation location, Func<SyntaxNode, bool> filter = null)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            // Shortcircuit if we're out of the possible bounds
            if (location.AbsoluteIndex < node.Position || node.EndPosition <= location.AbsoluteIndex)
            {
                return null;
            }

            foreach (var child in GetChildNodes(node))
            {
                var descendant = GetNodeAtLocation(child, location, filter);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            if (filter is null || filter(node))
            {
                return node;
            }

            return null;
        }

        public static IReadOnlyList<SyntaxNode> GetChildNodes(SyntaxNode node)
        {
            if (node is MarkupStartTagSyntax startTag)
            {
                return startTag.Children;
            }
            else if (node is MarkupEndTagSyntax endTag)
            {
                return endTag.Children;
            }
            else if (node is MarkupTagHelperStartTagSyntax startTagHelper)
            {
                return startTagHelper.Children;
            }
            else if (node is MarkupTagHelperEndTagSyntax endTagHelper)
            {
                return endTagHelper.Children;
            }
            return node.ChildNodes();
        }
    }
}
