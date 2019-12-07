// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal static class TagHelperStaticMethods
    {
        // Internal for testing
        internal static IEnumerable<KeyValuePair<string, string>> StringifyAttributes(SyntaxList<RazorSyntaxNode> attributes)
        {
            var stringifiedAttributes = new List<KeyValuePair<string, string>>();

            for (var i = 0; i < attributes.Count; i++)
            {
                var attribute = attributes[i];
                if (attribute is MarkupTagHelperAttributeSyntax tagHelperAttribute)
                {
                    var name = tagHelperAttribute.Name.GetContent();
                    var value = tagHelperAttribute.Value?.GetContent() ?? string.Empty;
                    stringifiedAttributes.Add(new KeyValuePair<string, string>(name, value));
                }
                else if (attribute is MarkupMinimizedTagHelperAttributeSyntax minimizedTagHelperAttribute)
                {
                    var name = minimizedTagHelperAttribute.Name.GetContent();
                    stringifiedAttributes.Add(new KeyValuePair<string, string>(name, string.Empty));
                }
                else if (attribute is MarkupAttributeBlockSyntax markupAttribute)
                {
                    var name = markupAttribute.Name.GetContent();
                    var value = markupAttribute.Value?.GetContent() ?? string.Empty;
                    stringifiedAttributes.Add(new KeyValuePair<string, string>(name, value));
                }
                else if (attribute is MarkupMinimizedAttributeBlockSyntax minimizedMarkupAttribute)
                {
                    var name = minimizedMarkupAttribute.Name.GetContent();
                    stringifiedAttributes.Add(new KeyValuePair<string, string>(name, string.Empty));
                }
                else if (attribute is MarkupTagHelperDirectiveAttributeSyntax directiveAttribute)
                {
                    var name = directiveAttribute.FullName;
                    var value = directiveAttribute.Value?.GetContent() ?? string.Empty;
                    stringifiedAttributes.Add(new KeyValuePair<string, string>(name, value));
                }
                else if (attribute is MarkupMinimizedTagHelperDirectiveAttributeSyntax minimizedDirectiveAttribute)
                {
                    var name = minimizedDirectiveAttribute.FullName;
                    stringifiedAttributes.Add(new KeyValuePair<string, string>(name, string.Empty));
                }
            }

            return stringifiedAttributes;
        }

        internal static bool TryGetElementInfo(SyntaxNode element, out SyntaxToken containingTagNameToken, out SyntaxList<RazorSyntaxNode> attributeNodes)
        {
            if (element is MarkupStartTagSyntax startTag)
            {
                containingTagNameToken = startTag.Name;
                attributeNodes = startTag.Attributes;
                return true;
            }

            if (element is MarkupTagHelperStartTagSyntax startTagHelper)
            {
                containingTagNameToken = startTagHelper.Name;
                attributeNodes = startTagHelper.Attributes;
                return true;
            }

            containingTagNameToken = null;
            attributeNodes = default;
            return false;
        }

        internal static bool TryGetAttributeInfo(SyntaxNode attribute, out SyntaxToken containingTagNameToken, out string selectedAttributeName, out SyntaxList<RazorSyntaxNode> attributeNodes)
        {
            if ((attribute is MarkupMiscAttributeContentSyntax ||
                attribute is MarkupMinimizedAttributeBlockSyntax ||
                attribute is MarkupAttributeBlockSyntax ||
                attribute is MarkupTagHelperAttributeSyntax ||
                attribute is MarkupMinimizedTagHelperAttributeSyntax ||
                attribute is MarkupTagHelperDirectiveAttributeSyntax ||
                attribute is MarkupMinimizedTagHelperDirectiveAttributeSyntax) &&
                TryGetElementInfo(attribute.Parent, out containingTagNameToken, out attributeNodes))
            {
                if (attribute is MarkupTagHelperAttributeSyntax)
                {
                    selectedAttributeName = ((MarkupTagHelperAttributeSyntax)attribute).Name.GetContent();
                }
                else {
                    selectedAttributeName = null;
                }
                return true;
            }

            containingTagNameToken = null;
            selectedAttributeName = null;
            attributeNodes = default;
            return false;
        }
    }
}