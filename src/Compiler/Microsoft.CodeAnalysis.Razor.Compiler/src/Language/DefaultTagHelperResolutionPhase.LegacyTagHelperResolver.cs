// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

internal partial class DefaultTagHelperResolutionPhase
{
    private sealed class LegacyTagHelperResolver : TagHelperResolver
    {
        public override void BuildTagHelper(
            TagHelperIntermediateNode tagHelperNode,
            TagHelperBodyIntermediateNode bodyNode,
            ElementOrTagHelperIntermediateNode elementNode,
            TagHelperBinding binding,
            RazorSourceDocument sourceDocument,
            in ResolutionContext context)
        {
            // Add body node first (like the original lowering).
            tagHelperNode.Children.Add(bodyNode);

            throw new NotImplementedException("Legacy tag helper construction not yet implemented.");
        }

        public override void ConvertToPlainElement(IntermediateNode parent, int index, ElementOrTagHelperIntermediateNode elementNode)
        {
            // Remove the wrapper and promote its children to the parent,
            // handling unresolved attributes and HtmlAttributeIntermediateNode appropriately.
            parent.Children.RemoveAt(index);

            var insertIndex = index;
            foreach (var child in elementNode.Children)
            {
                if (child is MarkupOrTagHelperAttributeIntermediateNode unresolvedAttr)
                {
                    // Use the pre-lowered AsMarkupAttribute fallback.
                    if (unresolvedAttr.AsMarkupAttribute is MarkupElementIntermediateNode container)
                    {
                        foreach (var lowered in container.Children)
                        {
                            parent.Children.Insert(insertIndex++, lowered);
                        }
                    }
                    else if (unresolvedAttr.AsMarkupAttribute != null)
                    {
                        parent.Children.Insert(insertIndex++, unresolvedAttr.AsMarkupAttribute);
                    }
                    continue;
                }

                if (child is HtmlAttributeIntermediateNode htmlAttr)
                {
                    insertIndex = UnwrapHtmlAttribute(parent, insertIndex, htmlAttr);
                    continue;
                }

                parent.Children.Insert(insertIndex++, child);
            }

            MergeAdjacentHtmlContent(parent, index, insertIndex);
        }

        private static int UnwrapHtmlAttribute(IntermediateNode parent, int insertIndex, HtmlAttributeIntermediateNode htmlAttr)
        {
            var attrName = htmlAttr.AttributeName ?? string.Empty;
            var isDataDash = attrName.StartsWith("data-", StringComparison.OrdinalIgnoreCase);
            var hasDynamicChildren = false;
            foreach (var attrChild in htmlAttr.Children)
            {
                if (attrChild is CSharpExpressionAttributeValueIntermediateNode or
                    CSharpCodeAttributeValueIntermediateNode or
                    CSharpExpressionIntermediateNode)
                {
                    hasDynamicChildren = true;
                    break;
                }
            }

            if (isDataDash)
            {
                return UnwrapDataDashAttribute(parent, insertIndex, htmlAttr, hasDynamicChildren);
            }

            // Non-data-dash attributes: check if they should be flattened to text.
            // - Literal value (has HtmlAttributeValueIntermediateNode children) -> flatten
            // - Minimized (no children, no = in prefix) -> flatten
            // - Empty value with = (no children, = in prefix) -> keep as HtmlAttributeIntermediateNode
            // - Dynamic content -> keep as HtmlAttributeIntermediateNode
            var shouldFlatten = false;
            if (htmlAttr.Children.Count > 0)
            {
                // Check if all children are literal.
                shouldFlatten = true;
                foreach (var attrChild in htmlAttr.Children)
                {
                    if (attrChild is not HtmlAttributeValueIntermediateNode and
                        not HtmlContentIntermediateNode)
                    {
                        shouldFlatten = false;
                        break;
                    }
                }
            }
            else if (!(htmlAttr.Prefix ?? string.Empty).Contains('='))
            {
                // 0 children: minimized (no =) -> flatten; empty value (has =) -> keep.
                shouldFlatten = true;
            }

            if (shouldFlatten)
            {
                // Flatten to HtmlContent (merges with surrounding text).
                var attrContent = FlattenAttributeToHtml(htmlAttr);
                if (!string.IsNullOrEmpty(attrContent))
                {
                    var htmlContent = new HtmlContentIntermediateNode() { Source = htmlAttr.Source };
                    htmlContent.Children.Add(new HtmlIntermediateToken(attrContent, htmlAttr.Source));
                    parent.Children.Insert(insertIndex++, htmlContent);
                }
            }
            else
            {
                // Empty or dynamic: keep as HtmlAttributeIntermediateNode (BeginWriteAttribute pattern).
                parent.Children.Insert(insertIndex++, htmlAttr);
            }

            return insertIndex;
        }

        private static int UnwrapDataDashAttribute(IntermediateNode parent, int insertIndex, HtmlAttributeIntermediateNode htmlAttr, bool hasDynamicChildren)
        {
            if (!hasDynamicChildren)
            {
                // Data-dash with only literal content: flatten to HtmlContent.
                var attrContent = FlattenAttributeToHtml(htmlAttr);
                if (!string.IsNullOrEmpty(attrContent))
                {
                    var htmlContent = new HtmlContentIntermediateNode() { Source = htmlAttr.Source };
                    htmlContent.Children.Add(new HtmlIntermediateToken(attrContent, htmlAttr.Source));
                    parent.Children.Insert(insertIndex++, htmlContent);
                }

                return insertIndex;
            }

            // Data-dash with dynamic content: flatten prefix to HtmlContent,
            // then promote each child (unwrapping HtmlAttributeValue to HtmlContent).
            var prefix = htmlAttr.Prefix ?? string.Empty;
            if (prefix.Length > 0)
            {
                var prefixHtml = new HtmlContentIntermediateNode() { Source = htmlAttr.Source };
                prefixHtml.Children.Add(new HtmlIntermediateToken(prefix, htmlAttr.Source));
                parent.Children.Insert(insertIndex++, prefixHtml);
            }
            foreach (var attrChild in htmlAttr.Children)
            {
                if (attrChild is HtmlAttributeValueIntermediateNode attrValue)
                {
                    var (content, tokenSource) = CollectAttributeValueContent(attrValue);
                    if (content.Length > 0)
                    {
                        var hc = new HtmlContentIntermediateNode() { Source = tokenSource };
                        hc.Children.Add(new HtmlIntermediateToken(content, tokenSource));
                        parent.Children.Insert(insertIndex++, hc);
                    }
                }
                else if (attrChild is CSharpExpressionAttributeValueIntermediateNode exprAttrValue)
                {
                    // Unwrap: prefix as text, inner expression
                    if (!string.IsNullOrEmpty(exprAttrValue.Prefix))
                    {
                        var pHtml = new HtmlContentIntermediateNode();
                        pHtml.Children.Add(new HtmlIntermediateToken(exprAttrValue.Prefix, source: null));
                        parent.Children.Insert(insertIndex++, pHtml);
                    }
                    foreach (var innerChild in exprAttrValue.Children)
                    {
                        if (innerChild is CSharpIntermediateToken csharpToken)
                        {
                            var expr = new CSharpExpressionIntermediateNode() { Source = csharpToken.Source };
                            expr.Children.Add(csharpToken);
                            parent.Children.Insert(insertIndex++, expr);
                        }
                        else
                        {
                            parent.Children.Insert(insertIndex++, innerChild);
                        }
                    }
                }
                else
                {
                    parent.Children.Insert(insertIndex++, attrChild);
                }
            }
            var suffix = htmlAttr.Suffix ?? string.Empty;
            if (suffix.Length > 0)
            {
                var suffixHtml = new HtmlContentIntermediateNode();
                suffixHtml.Children.Add(new HtmlIntermediateToken(suffix, source: null));
                parent.Children.Insert(insertIndex++, suffixHtml);
            }

            return insertIndex;
        }

        private static void MergeAdjacentHtmlContent(IntermediateNode parent, int index, int insertIndex)
        {
            // After unwrapping, aggressively merge all adjacent HtmlContent nodes in the
            // affected range (and at boundaries with surrounding content).
            // Use unconditional merging since flattened attributes may have non-adjacent source spans.
            var mergeStart = Math.Max(0, index - 1);
            var mergeEnd = Math.Min(parent.Children.Count - 1, insertIndex);

            for (var i = mergeStart; i < mergeEnd && i < parent.Children.Count - 1; )
            {
                if (parent.Children[i] is HtmlContentIntermediateNode current &&
                    parent.Children[i + 1] is HtmlContentIntermediateNode next &&
                    CanMerge(current, next))
                {
                    // Merge next into current.
                    foreach (var child in next.Children)
                    {
                        current.Children.Add(child);
                    }
                    if (current.Source is SourceSpan currentSource && next.Source is SourceSpan nextSource)
                    {
                        current.Source = new SourceSpan(
                            currentSource.FilePath,
                            currentSource.AbsoluteIndex,
                            currentSource.LineIndex,
                            currentSource.CharacterIndex,
                            (nextSource.AbsoluteIndex + nextSource.Length) - currentSource.AbsoluteIndex,
                            nextSource.LineCount,
                            nextSource.EndCharacterIndex);
                    }
                    else if (current.Source == null)
                    {
                        current.Source = next.Source;
                    }
                    parent.Children.RemoveAt(i + 1);
                    mergeEnd--;
                }
                else
                {
                    i++;
                }
            }
        }

        /// <summary>
        /// Flattens an HtmlAttributeIntermediateNode back to its original HTML text representation:
        /// prefix + child content + suffix.
        /// </summary>
        private static string FlattenAttributeToHtml(HtmlAttributeIntermediateNode htmlAttr)
        {
            using var _ = StringBuilderPool.GetPooledObject(out var sb);

            sb.Append(htmlAttr.Prefix ?? string.Empty);

            foreach (var child in htmlAttr.Children)
            {
                if (child is HtmlAttributeValueIntermediateNode attrValue)
                {
                    sb.Append(CollectAttributeValueContent(attrValue).Content);
                }
                else if (child is IntermediateToken directToken)
                {
                    sb.Append(directToken.Content);
                }
                else if (child is HtmlContentIntermediateNode htmlContent)
                {
                    foreach (var token in htmlContent.Children)
                    {
                        if (token is IntermediateToken intermediateToken)
                        {
                            sb.Append(intermediateToken.Content);
                        }
                    }
                }
                else if (child is CSharpExpressionAttributeValueIntermediateNode csharpAttrValue)
                {
                    sb.Append(csharpAttrValue.Prefix ?? string.Empty);
                    foreach (var exprChild in csharpAttrValue.Children)
                    {
                        if (exprChild is IntermediateToken exprToken)
                        {
                            sb.Append(exprToken.Content);
                        }
                        else if (exprChild is CSharpExpressionIntermediateNode innerExpr)
                        {
                            foreach (var innerToken in innerExpr.Children)
                            {
                                if (innerToken is IntermediateToken t)
                                {
                                    sb.Append(t.Content);
                                }
                            }
                        }
                    }
                }
            }

            sb.Append(htmlAttr.Suffix ?? string.Empty);

            return sb.ToString();
        }

        private static bool CanMerge(HtmlContentIntermediateNode a, HtmlContentIntermediateNode b)
        {
            if (a.Source == null || b.Source == null)
            {
                return true;
            }

            if (a.Source is SourceSpan aSource && b.Source is SourceSpan bSource)
            {
                return aSource.FilePath == bSource.FilePath &&
                       aSource.AbsoluteIndex + aSource.Length == bSource.AbsoluteIndex;
            }

            return false;
        }
    }
}
