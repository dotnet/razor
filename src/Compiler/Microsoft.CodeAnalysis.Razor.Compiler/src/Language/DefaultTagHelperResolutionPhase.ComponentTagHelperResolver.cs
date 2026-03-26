// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

internal partial class DefaultTagHelperResolutionPhase
{
    private sealed class ComponentTagHelperResolver : TagHelperResolver
    {
        public override void AddMatchedElementDiagnostics(
            TagHelperIntermediateNode tagHelperNode,
            ElementOrTagHelperIntermediateNode elementNode,
            TagHelperBinding binding,
            in ResolutionContext context)
        {
            var tagName = elementNode.TagName;

            // Add RZ10012 for elements that look like components but didn't match a Component
            // or ChildContent tag helper. Catch-all directive attribute helpers (@key, @ref,
            // @rendermode) match any element, not just components, so they don't count.
            if (context.DocumentNode != null &&
                !context.DocumentNode.Options.SuppressPrimaryMethodBody &&
                !string.IsNullOrEmpty(tagName) &&
                DefaultRazorIntermediateNodeLoweringPhase.LooksLikeAComponentName(context.DocumentNode, tagName) &&
                !binding.TagHelpers.Any(static th => th.Kind.IsComponentOrChildContentKind))
            {
                tagHelperNode.AddDiagnostic(
                    ComponentDiagnosticFactory.Create_UnexpectedMarkupElement(tagName, elementNode.StartTagSpan ?? elementNode.Source));
            }

            // Check for case mismatch between start and end tag names.
            if (elementNode.EndTagName != null)
            {
                var startTagName = elementNode.TagName;
                var endTagName = elementNode.EndTagName;
                if (!string.Equals(startTagName, endTagName, StringComparison.Ordinal))
                {
                    tagHelperNode.AddDiagnostic(
                        ComponentDiagnosticFactory.Create_InconsistentStartAndEndTagName(startTagName, endTagName, elementNode.EndTagSpan));
                }
            }
        }

        public override void AddUnmatchedElementDiagnostic(
            IntermediateNode convertedNode,
            ElementOrTagHelperIntermediateNode originalNode,
            DocumentIntermediateNode documentNode)
        {
            if (documentNode != null &&
                !documentNode.Options.SuppressPrimaryMethodBody &&
                !string.IsNullOrEmpty(originalNode.TagName) &&
                DefaultRazorIntermediateNodeLoweringPhase.LooksLikeAComponentName(documentNode, originalNode.TagName))
            {
                convertedNode.AddDiagnostic(
                    ComponentDiagnosticFactory.Create_UnexpectedMarkupElement(originalNode.TagName, originalNode.StartTagSpan ?? originalNode.Source));
            }
        }

        /// <summary>
        /// Builds a <see cref="TagHelperIntermediateNode"/> from a component element. Iterates
        /// through the element's children, converting unresolved and HTML attributes to tag helper
        /// attribute nodes, and adding remaining children (body content) to the body node.
        /// </summary>
        public override void BuildTagHelper(
            TagHelperIntermediateNode tagHelperNode,
            TagHelperBodyIntermediateNode bodyNode,
            ElementOrTagHelperIntermediateNode elementNode,
            TagHelperBinding binding,
            RazorSourceDocument sourceDocument,
            in ResolutionContext context)
        {
            tagHelperNode.Children.Add(bodyNode);

            throw new NotImplementedException("Component tag helper construction not yet implemented.");
        }

        /// <summary>
        /// Converts a non-tag-helper element to <see cref="MarkupElementIntermediateNode"/> (component files).
        /// Preserves element structure (tag name, source span). Unresolved attributes are replaced with their
        /// <see cref="MarkupOrTagHelperAttributeIntermediateNode.AsMarkupAttribute"/> (full attribute form).
        /// </summary>
        public override void ConvertToPlainElement(IntermediateNode parent, int index, ElementOrTagHelperIntermediateNode elementNode)
        {
            var markupElement = new MarkupElementIntermediateNode()
            {
                Source = elementNode.Source,
                TagName = elementNode.TagName,
            };

            // Move diagnostics.
            if (elementNode.HasDiagnostics)
            {
                foreach (var diagnostic in elementNode.Diagnostics)
                {
                    markupElement.AddDiagnostic(diagnostic);
                }
            }

            // Transfer all children, lowering unresolved attributes to their fallback form.
            foreach (var child in elementNode.Children)
            {
                if (child is MarkupOrTagHelperAttributeIntermediateNode unresolvedAttr)
                {
                    // Use the pre-lowered AsMarkupAttribute fallback form.
                    if (unresolvedAttr.AsMarkupAttribute != null)
                    {
                        markupElement.Children.Add(unresolvedAttr.AsMarkupAttribute);
                    }
                }
                else
                {
                    markupElement.Children.Add(child);
                }
            }

            parent.Children[index] = markupElement;
        }
    }
}
