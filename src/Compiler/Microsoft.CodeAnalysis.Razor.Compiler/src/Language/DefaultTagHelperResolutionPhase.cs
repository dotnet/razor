// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

/// <summary>
/// A phase that runs after lowering and tag helper discovery to resolve
/// <see cref="ElementOrTagHelperIntermediateNode"/> nodes into either
/// <see cref="TagHelperIntermediateNode"/> (if the element matches a tag helper)
/// or the appropriate plain element nodes (if it does not).
/// Works with IR nodes only -- no syntax tree access.
/// </summary>
internal partial class DefaultTagHelperResolutionPhase : RazorEnginePhaseBase
{
    private TagHelperResolver _resolver;
    /// <summary>
    /// Entry point: resolves all unresolved <see cref="ElementOrTagHelperIntermediateNode"/> nodes
    /// in the IR tree. For each, matches against tag helper bindings and either converts to a
    /// <see cref="TagHelperIntermediateNode"/> or unwraps to plain markup. A final
    /// <see cref="UnwrapAllElements"/> pass handles any remaining unresolved nodes.
    /// </summary>
    protected override RazorCodeDocument ExecuteCore(RazorCodeDocument codeDocument, CancellationToken cancellationToken)
    {
        var documentNode = codeDocument.GetDocumentNode();
        if (documentNode == null)
        {
            return codeDocument;
        }

        _resolver = codeDocument.FileKind.IsComponent() || codeDocument.FileKind.IsComponentImport()
            ? new ComponentTagHelperResolver()
            : new LegacyTagHelperResolver();

        var sourceDocument = codeDocument.Source;

        var tagHelperContext = codeDocument.GetTagHelperContext();
        var syntaxTree = codeDocument.GetPreTagHelperSyntaxTree() ?? codeDocument.GetSyntaxTree();
        var parserOptions = syntaxTree?.Options;

        if (tagHelperContext == null || tagHelperContext.TagHelpers is [])
        {
            // No tag helpers discovered - unwrap all ElementOrTagHelper nodes to their fallback.
            UnwrapAllElements(documentNode, documentNode);

            // Still need to set referenced tag helpers for downstream phases.
            return codeDocument.WithReferencedTagHelpers([]);
        }

        var binder = tagHelperContext.GetBinder();
        var prefix = tagHelperContext.Prefix;

        using var usedHelpers = new TagHelperCollection.Builder();
        var context = new ResolutionContext(sourceDocument, parserOptions, documentNode);
        ResolveElements(documentNode, binder, prefix, usedHelpers, in context);

        // Add tag helper descriptor validation diagnostics (e.g. RZ3003).
        using var descriptorDiagnostics = new PooledArrayBuilder<RazorDiagnostic>();
        foreach (var descriptor in tagHelperContext.TagHelpers)
        {
            descriptor.AppendAllDiagnostics(ref descriptorDiagnostics.AsRef());
        }

        foreach (var diagnostic in descriptorDiagnostics)
        {
            documentNode.AddDiagnostic(diagnostic);
        }

        return codeDocument.WithReferencedTagHelpers(usedHelpers.ToCollection());
    }

    /// <summary>
    /// Holds ambient state needed during element resolution. Passed by ref to avoid
    /// threading many parameters through the call chain.
    /// </summary>
    private readonly struct ResolutionContext
    {
        public readonly RazorSourceDocument SourceDocument;
        public readonly RazorParserOptions ParserOptions;
        public readonly DocumentIntermediateNode DocumentNode;

        public ResolutionContext(RazorSourceDocument sourceDocument, RazorParserOptions parserOptions, DocumentIntermediateNode documentNode)
        {
            SourceDocument = sourceDocument;
            ParserOptions = parserOptions;
            DocumentNode = documentNode;
        }
    }

    private void ResolveElements(IntermediateNode node, TagHelperBinder binder, string prefix, TagHelperCollection.Builder usedHelpers, in ResolutionContext context)
    {
        // Process children in reverse order since we may be replacing nodes.
        for (var i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = node.Children[i];

            if (child is ElementOrTagHelperIntermediateNode elementNode)
            {
                // Resolve THIS element first. If it becomes a component tag helper,
                // BuildComponentTagHelper moves body children into the body node,
                // and the post-build re-resolution pass handles them with parent context.
                // This is important for child content elements like <ChildContent> that
                // need to know their parent tag helper to bind correctly.
                ResolveElement(node, i, elementNode, binder, prefix, usedHelpers, in context);
            }
            else
            {
                // For non-element nodes, recurse into children normally.
                ResolveElements(child, binder, prefix, usedHelpers, in context);
            }
        }
    }

    /// <summary>
    /// Resolves a single <see cref="ElementOrTagHelperIntermediateNode"/> by checking its tag
    /// name and attributes against the <paramref name="binder"/>. If it matches a tag helper,
    /// replaces it with a <see cref="TagHelperIntermediateNode"/>. Otherwise, delegates to
    /// the resolver to convert the element back to plain HTML markup.
    /// </summary>
    private void ResolveElement(
        IntermediateNode parent,
        int index,
        ElementOrTagHelperIntermediateNode elementNode,
        TagHelperBinder binder,
        string prefix,
        TagHelperCollection.Builder usedHelpers,
        in ResolutionContext context,
        TagHelperIntermediateNode tagHelperParent = null)
    {
        var tagName = elementNode.TagName;

        // Check for escaped tag helpers (<!tagname>) - these should NOT be matched.
        if (elementNode.IsEscaped)
        {
            ConvertToPlainElementAndResolve(parent, index, elementNode, binder, prefix, usedHelpers, in context, emitDiagnostics: false);
            return;
        }

        // Use pre-extracted attribute data for binding.
        var attributes = elementNode.AttributeData;

        // End-tag-only elements (e.g. </body> without matching <body>) should not match tag helpers.
        if (elementNode.StartTagNameSpan == null && !elementNode.IsSelfClosing && attributes.IsEmpty)
        {
            TryAddMalformedEndTagDiagnostic(elementNode, tagName, binder, attributes, parent, tagHelperParent);

            _resolver.ConvertToPlainElement(parent, index, elementNode);

            // Transfer element diagnostics to the converted/unwrapped node.
            // ConvertToMarkupElement already copies diagnostics, but UnwrapElement does not,
            // so this is needed for the legacy path.
            if (elementNode.HasDiagnostics)
            {
                parent.Children[index].AddDiagnosticsFromNode(elementNode);
            }
            return;
        }

        var (parentTagName, parentIsTagHelper) = GetParentTagInfo(parent, tagHelperParent);
        var binding = binder.GetBinding(tagName, attributes, parentTagName, parentIsTagHelper);
        if (binding == null)
        {
            ConvertToPlainElementAndResolve(parent, index, elementNode, binder, prefix, usedHelpers, in context);
            return;
        }

        // It IS a tag helper. Track the used helpers.
        foreach (var th in binding.TagHelpers)
        {
            usedHelpers.Add(th);
        }

        // Determine tag name (strip prefix if present).
        var resolvedTagName = tagName;
        if (prefix != null && resolvedTagName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            resolvedTagName = resolvedTagName.Substring(prefix.Length);
        }

        // Determine TagMode from IR properties.
        var tagMode = GetTagMode(elementNode, binding);

        // Create the TagHelperIntermediateNode.
        var tagHelperNode = new TagHelperIntermediateNode()
        {
            TagName = resolvedTagName,
            TagMode = tagMode,
            Source = elementNode.Source,
            TagHelpers = binding.TagHelpers,
            StartTagSpan = elementNode.StartTagNameSpan,
        };

        // Add resolver-specific diagnostics (e.g. RZ10012 for component-like elements,
        // case mismatch between start/end tags).
        _resolver.AddMatchedElementDiagnostics(tagHelperNode, elementNode, binding, in context);

        // Check if resolved tag name is a void element (handles prefixed elements like th:input).
        var isResolvedVoidElement = elementNode.IsVoidElement || Legacy.ParserHelpers.VoidElements.Contains(resolvedTagName);

        // Directive-attribute-only matches (e.g. only @bind, @onclick) don't produce
        // structural diagnostics -- the rewriter doesn't emit them for such matches.
        if (!IsDirectiveAttributeOnly(binding))
        {
            // Missing close angle diagnostics (RZ1035) -- emitted before RZ1034 to match rewriter ordering.
            if (elementNode.HasMissingCloseAngle)
            {
                var diagSource = elementNode.StartTagNameSpan ?? elementNode.Source;
                if (diagSource is SourceSpan ds)
                {
                    tagHelperNode.AddDiagnostic(
                        RazorDiagnosticFactory.CreateParsing_TagHelperMissingCloseAngle(ds, tagName));
                }
            }
            if (elementNode.HasMissingEndCloseAngle && elementNode.EndTagSpan is SourceSpan endDs)
            {
                tagHelperNode.AddDiagnostic(
                    RazorDiagnosticFactory.CreateParsing_TagHelperMissingCloseAngle(endDs, elementNode.EndTagName ?? tagName));
            }

            // Structural diagnostics: RZ1034 (malformed tag helper without end tag).
            // Only for non-void, non-self-closing elements without end tags,
            // and only when TagMode expects an end tag (StartTagAndEndTag).
            if (!elementNode.HasEndTag && !elementNode.IsSelfClosing && !isResolvedVoidElement
                && tagMode == TagMode.StartTagAndEndTag)
            {
                var diagSource = elementNode.StartTagNameSpan ?? elementNode.Source;
                if (diagSource is SourceSpan ds)
                {
                    tagHelperNode.AddDiagnostic(
                        RazorDiagnosticFactory.CreateParsing_TagHelperFoundMalformedTagHelper(ds, tagName));
                }
            }

            // RZ1042: void element without end tag (parser treated it as void).
            // Fires when a void-element-named tag helper has no end tag and
            // TagMode is StartTagAndEndTag (meaning the binding expected an end tag
            // but the parser treated the element as void). Elements with TagMode
            // StartTagOnly (e.g., TagStructure.WithoutEndTag) are handled without
            // diagnostics since the tag structure explicitly permits no end tag.
            if (isResolvedVoidElement && !elementNode.HasEndTag
                && tagMode == TagMode.StartTagAndEndTag)
            {
                var diagSource = elementNode.StartTagNameSpan ?? elementNode.Source;
                if (diagSource is SourceSpan ds)
                {
                    tagHelperNode.AddDiagnostic(
                        RazorDiagnosticFactory.CreateParsing_VoidElement(ds, tagName));
                }
            }
        }

        // Check for inconsistent TagStructure across bound rules (RZ2011).
        ValidateConsistentTagStructure(tagHelperNode, binding, elementNode, tagName);

        // Build body and attributes.
        var bodyNode = new TagHelperBodyIntermediateNode();

        _resolver.BuildTagHelper(tagHelperNode, bodyNode, elementNode, binding, context.SourceDocument, in context);

        // After building the tag helper, resolve any body children that are still
        // ElementOrTagHelperIntermediateNode. Pass the tagHelperNode as parent so the
        // binder can see the parent tag name. This is needed for:
        // - Components: child content matching (e.g., Found/NotFound inside Router)
        // - Legacy tag helpers: RequireParentTag matching (e.g., <td> inside <tr>)
        var tagHelperParentForBody = tagHelperNode;
        for (var i = bodyNode.Children.Count - 1; i >= 0; i--)
        {
            var bodyChild = bodyNode.Children[i];

            if (bodyChild is ElementOrTagHelperIntermediateNode bodyElementNode)
            {
                // Resolve the element first with parent context. This is critical because
                // ResolveElement will call BuildComponentTagHelper which moves the element's
                // own children into a body node and then recursively resolves them with the
                // correct parent tag helper context. If we called ResolveElements first, it
                // would descend into the element's children and prematurely resolve them
                // without knowing the parent tag helper (e.g., Found/NotFound inside Router
                // need to know Router is their parent to be matched as child content).
                ResolveElement(bodyNode, i, bodyElementNode, binder, prefix, usedHelpers, in context, tagHelperParentForBody);
            }
            else
            {
                ResolveElements(bodyChild, binder, prefix, usedHelpers, in context);
            }
        }

        // Note: RZ1033 (tag helper must not have an end tag when TagStructure is WithoutEndTag)
        // is NOT emitted here. The ElementOrTagHelperIntermediateNode represents a matched
        // start/end tag pair. RZ1033 is only for orphan end tags (end tags without a matching
        // start tag on the tracker stack). For matched pairs like <component ...></component>,
        // the rewriter handles them normally. The rewriter (which still runs after this phase)
        // will emit RZ1033 for orphan end tags.

        // Check AllowedChildren constraints (RZ2009, RZ2010).
        ValidateAllowedChildren(tagHelperNode, bodyNode, binding, prefix);

        // Replace the ElementOrTagHelper with the TagHelperIntermediateNode.
        parent.Children[index] = tagHelperNode;

        // For StartTagOnly elements, body content from the original element
        // belongs to the parent, not the tag helper. Promote it.
        if (tagHelperNode.TagMode == TagMode.StartTagOnly)
        {
            var startTagEndIdx = elementNode.StartTagEndIndex;
            var bodyEndIdx = elementNode.BodyEndIndex;

            if (startTagEndIdx >= 0 && bodyEndIdx >= 0)
            {
                var insertIdx = index + 1;
                for (var i = startTagEndIdx; i < bodyEndIdx; i++)
                {
                    parent.Children.Insert(insertIdx++, elementNode.Children[i]);
                }
            }
        }
    }

    /// <summary>
    /// Converts an unmatched element to plain markup, emits resolver-specific diagnostics,
    /// and recursively resolves any resulting children that may be tag helpers.
    /// </summary>
    private void ConvertToPlainElementAndResolve(
        IntermediateNode parent, int index,
        ElementOrTagHelperIntermediateNode elementNode,
        TagHelperBinder binder, string prefix,
        TagHelperCollection.Builder usedHelpers,
        in ResolutionContext context,
        bool emitDiagnostics = true)
    {
        var childCountBefore = parent.Children.Count;
        _resolver.ConvertToPlainElement(parent, index, elementNode);
        var resultCount = parent.Children.Count - childCountBefore + 1; // +1 because the original was removed

        if (emitDiagnostics && resultCount > 0)
        {
            _resolver.AddUnmatchedElementDiagnostic(parent.Children[index], elementNode, context.DocumentNode);
        }

        for (var j = index + resultCount - 1; j >= index; j--)
        {
            if (j < parent.Children.Count)
            {
                if (parent.Children[j] is ElementOrTagHelperIntermediateNode promotedElement)
                {
                    ResolveElement(parent, j, promotedElement, binder, prefix, usedHelpers, in context);
                }
                else
                {
                    ResolveElements(parent.Children[j], binder, prefix, usedHelpers, in context);
                }
            }
        }
    }

    private static TagMode GetTagMode(ElementOrTagHelperIntermediateNode elementNode, TagHelperBinding binding)
    {
        if (elementNode.IsSelfClosing)
        {
            return TagMode.SelfClosing;
        }

        var hasDirectiveAttribute = false;
        foreach (var boundRulesInfo in binding.AllBoundRules)
        {
            var nonDefaultRule = boundRulesInfo.Rules.FirstOrDefault(static rule => rule.TagStructure != TagStructure.Unspecified);
            if (nonDefaultRule?.TagStructure == TagStructure.WithoutEndTag)
            {
                return TagMode.StartTagOnly;
            }

            var descriptor = boundRulesInfo.Descriptor;
            if (descriptor.IsAnyComponentDocumentTagHelper() && !descriptor.IsComponentOrChildContentTagHelper())
            {
                hasDirectiveAttribute = true;
            }
        }

        if (hasDirectiveAttribute && elementNode.IsVoidElement && !elementNode.HasEndTag)
        {
            return TagMode.StartTagOnly;
        }

        return TagMode.StartTagAndEndTag;
    }

    private static void ValidateAllowedChildren(
        TagHelperIntermediateNode tagHelperNode,
        TagHelperBodyIntermediateNode bodyNode,
        TagHelperBinding binding,
        string prefix)
    {
        // Collect allowed child tag names from all descriptors.
        using var allowedNames = new PooledArrayBuilder<string>();
        foreach (var th in binding.TagHelpers)
        {
            foreach (var childTag in th.AllowedChildTags)
            {
                allowedNames.Add(childTag.Name);
            }
        }

        if (allowedNames.Count == 0)
        {
            return; // No AllowedChildTags constraints
        }

        var allowedChildrenString = string.Join(", ", allowedNames.ToArray());
        var parentTagName = tagHelperNode.TagName;

        foreach (var child in bodyNode.Children)
        {
            if (child is TagHelperIntermediateNode childTagHelper)
            {
                var childTagName = childTagHelper.TagName;
                if (!IsAllowedChild(childTagName, in allowedNames))
                {
                    childTagHelper.AddDiagnostic(
                        RazorDiagnosticFactory.CreateTagHelper_InvalidNestedTag(
                            child.Source ?? SourceSpan.Undefined, childTagName, parentTagName, allowedChildrenString));
                }
            }
            else if (child is MarkupElementIntermediateNode markupElement)
            {
                var childTagName = markupElement.TagName;
                // Strip prefix if present
                if (prefix != null && childTagName != null && childTagName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    childTagName = childTagName.Substring(prefix.Length);
                }
                if (childTagName != null && !IsAllowedChild(childTagName, in allowedNames))
                {
                    markupElement.AddDiagnostic(
                        RazorDiagnosticFactory.CreateTagHelper_InvalidNestedTag(
                            child.Source ?? SourceSpan.Undefined, childTagName, parentTagName, allowedChildrenString));
                }
            }
            else if (child is HtmlContentIntermediateNode htmlContent)
            {
                // Check if content is non-whitespace
                var hasNonWhitespace = false;
                foreach (var token in htmlContent.Children)
                {
                    if (token is IntermediateToken t && !string.IsNullOrWhiteSpace(t.Content))
                    {
                        hasNonWhitespace = true;
                        break;
                    }
                }
                if (hasNonWhitespace)
                {
                    htmlContent.AddDiagnostic(
                        RazorDiagnosticFactory.CreateTagHelper_CannotHaveNonTagContent(
                            child.Source ?? SourceSpan.Undefined, parentTagName, allowedChildrenString));
                }
            }
            else if (child is CSharpExpressionIntermediateNode or CSharpCodeIntermediateNode)
            {
                child.AddDiagnostic(
                    RazorDiagnosticFactory.CreateTagHelper_CannotHaveNonTagContent(
                        child.Source ?? SourceSpan.Undefined, parentTagName, allowedChildrenString));
            }
        }
    }

    private static bool IsAllowedChild(string tagName, in PooledArrayBuilder<string> allowedNames)
    {
        foreach (var name in allowedNames)
        {
            if (string.Equals(tagName, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Final pass that finds remaining <see cref="ElementOrTagHelperIntermediateNode"/> nodes not
    /// resolved by tag helper matching. Converts each to a plain element using the resolver.
    /// Recursively processes the tree to handle nested elements.
    /// </summary>
    private void UnwrapAllElements(IntermediateNode node, DocumentIntermediateNode documentNode = null)
    {
        if (node is DocumentIntermediateNode doc)
        {
            documentNode = doc;
        }

        for (var i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = node.Children[i];
            UnwrapAllElements(child, documentNode);

            if (child is ElementOrTagHelperIntermediateNode elementNode)
            {
                _resolver.ConvertToPlainElement(node, i, elementNode);
                _resolver.AddUnmatchedElementDiagnostic(node.Children[i], elementNode, documentNode);
            }
        }
    }

    /// <summary>
    /// Collects the text content and first child source from an <see cref="HtmlAttributeValueIntermediateNode"/>,
    /// concatenating the prefix with all token content.
    /// </summary>
    private static (string Content, SourceSpan? Source) CollectAttributeValueContent(HtmlAttributeValueIntermediateNode attrValue)
    {
        var content = attrValue.Prefix ?? string.Empty;
        foreach (var token in attrValue.Children)
        {
            if (token is IntermediateToken intermediateToken)
            {
                content += intermediateToken.Content;
            }
        }

        var source = attrValue.Children.Count > 0 ? attrValue.Children[0].Source : attrValue.Source;
        return (content, source);
    }

    /// <summary>
    /// Returns the parent tag name and whether it's a tag helper, for use with the binder.
    /// Checks the explicit <paramref name="tagHelperParent"/> first (passed during body
    /// resolution), then falls back to checking if <paramref name="parent"/> is a tag helper node.
    /// </summary>
    private static (string TagName, bool IsTagHelper) GetParentTagInfo(
        IntermediateNode parent,
        TagHelperIntermediateNode tagHelperParent)
    {
        if (tagHelperParent != null)
        {
            return (tagHelperParent.TagName, true);
        }

        if (parent is TagHelperIntermediateNode parentTh)
        {
            return (parentTh.TagName, true);
        }

        return (null, false);
    }

    /// <summary>
    /// Checks if an end-tag-only element matches a tag helper and adds RZ1034 (malformed tag
    /// helper) to the element node if so. The diagnostic is added to <paramref name="elementNode"/>
    /// so that the subsequent convert/unwrap operation can transfer it to the replacement node.
    /// </summary>
    private static void TryAddMalformedEndTagDiagnostic(
        ElementOrTagHelperIntermediateNode elementNode,
        string tagName,
        TagHelperBinder binder,
        ImmutableArray<KeyValuePair<string, string>> attributes,
        IntermediateNode parent,
        TagHelperIntermediateNode tagHelperParent)
    {
        var (endParentTagName, endParentIsTagHelper) = GetParentTagInfo(parent, tagHelperParent);
        var endBinding = binder.GetBinding(tagName, attributes, endParentTagName, endParentIsTagHelper);
        if (endBinding == null)
        {
            return;
        }

        // Compute tag name position within end tag (after "</").
        var diagSource = elementNode.Source;
        if (elementNode.EndTagSpan is SourceSpan ets)
        {
            diagSource = new SourceSpan(
                ets.FilePath, ets.AbsoluteIndex + 2, ets.LineIndex, ets.CharacterIndex + 2,
                tagName.Length, 0, ets.CharacterIndex + 2 + tagName.Length);
        }

        if (diagSource is SourceSpan ds)
        {
            elementNode.AddDiagnostic(
                RazorDiagnosticFactory.CreateParsing_TagHelperFoundMalformedTagHelper(ds, tagName));
        }
    }

    /// <summary>
    /// Returns <c>true</c> if every tag helper in the binding is a directive attribute helper
    /// (e.g., <c>@bind</c>, <c>@onclick</c>) and not a component or child content tag helper.
    /// The rewriter does not produce structural diagnostics for directive-attribute-only matches.
    /// </summary>
    private static bool IsDirectiveAttributeOnly(TagHelperBinding binding)
    {
        foreach (var th in binding.TagHelpers)
        {
            if (!th.IsAnyComponentDocumentTagHelper() || th.IsComponentOrChildContentTagHelper())
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks for inconsistent <see cref="TagStructure"/> values across all bound rules
    /// in a tag helper binding, emitting RZ2011 if a conflict is found.
    /// </summary>
    private static void ValidateConsistentTagStructure(
        TagHelperIntermediateNode tagHelperNode,
        TagHelperBinding binding,
        ElementOrTagHelperIntermediateNode elementNode,
        string tagName)
    {
        TagStructure? baseStructure = null;
        string baseDisplayName = null;
        foreach (var boundRulesInfo in binding.AllBoundRules)
        {
            foreach (var rule in boundRulesInfo.Rules)
            {
                if (rule.TagStructure != TagStructure.Unspecified)
                {
                    if (baseStructure.HasValue && baseStructure != rule.TagStructure)
                    {
                        tagHelperNode.AddDiagnostic(
                            RazorDiagnosticFactory.CreateTagHelper_InconsistentTagStructure(
                                elementNode.Source ?? SourceSpan.Undefined,
                                baseDisplayName,
                                boundRulesInfo.Descriptor.DisplayName,
                                tagName));
                    }
                    baseStructure ??= rule.TagStructure;
                    baseDisplayName ??= boundRulesInfo.Descriptor.DisplayName;
                }
            }
        }
    }

    /// <summary>
    /// Abstract base class for tag helper resolution strategy. Subclasses handle
    /// either legacy (.cshtml) or component (.razor) element processing.
    /// </summary>
    private abstract class TagHelperResolver
    {
        /// <summary>
        /// Called when an element matches tag helpers and needs to be built into a
        /// <see cref="TagHelperIntermediateNode"/>.
        /// </summary>
        public abstract void BuildTagHelper(
            TagHelperIntermediateNode tagHelperNode,
            TagHelperBodyIntermediateNode bodyNode,
            ElementOrTagHelperIntermediateNode elementNode,
            TagHelperBinding binding,
            RazorSourceDocument sourceDocument,
            in ResolutionContext context);

        /// <summary>
        /// Called when an element does NOT match any tag helper and needs to be converted
        /// back to plain HTML markup nodes. Only handles conversion — does not recurse
        /// into children or emit diagnostics.
        /// </summary>
        public abstract void ConvertToPlainElement(
            IntermediateNode parent, int index,
            ElementOrTagHelperIntermediateNode elementNode);

        /// <summary>
        /// Called after an element is matched to a tag helper. Adds resolver-specific
        /// diagnostics such as RZ10012 (unrecognized component-like element) and case
        /// mismatch warnings. Default implementation does nothing.
        /// </summary>
        public virtual void AddMatchedElementDiagnostics(
            TagHelperIntermediateNode tagHelperNode,
            ElementOrTagHelperIntermediateNode elementNode,
            TagHelperBinding binding,
            in ResolutionContext context)
        {
        }

        /// <summary>
        /// Called after <see cref="ConvertToPlainElement"/> during the final unwrap pass
        /// to add resolver-specific diagnostics. Default implementation does nothing.
        /// </summary>
        public virtual void AddUnmatchedElementDiagnostic(
            IntermediateNode convertedNode,
            ElementOrTagHelperIntermediateNode originalNode,
            DocumentIntermediateNode documentNode)
        {
        }
    }

}
