// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;

namespace Microsoft.VisualStudio.Editor.Razor
{
    public abstract class TagHelperFactsService
    {
        public abstract TagHelperBinding GetTagHelperBinding(TagHelperDocumentContext documentContext, string tagName, IEnumerable<KeyValuePair<string, string>> attributes, string parentTag, bool parentIsTagHelper);

        public abstract IEnumerable<BoundAttributeDescriptor> GetBoundTagHelperAttributes(TagHelperDocumentContext documentContext, string attributeName, TagHelperBinding binding);

        public abstract IReadOnlyList<TagHelperDescriptor> GetTagHelpersGivenTag(TagHelperDocumentContext documentContext, string tagName, string parentTag);

        public abstract IReadOnlyList<TagHelperDescriptor> GetTagHelpersGivenParent(TagHelperDocumentContext documentContext, string parentTag);

        // Internal for testing
        internal virtual IEnumerable<KeyValuePair<string, string>> StringifyAttributes(SyntaxList<RazorSyntaxNode> attributes)
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
    }
}
