// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.Decompiler.CSharp.Syntax;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;

internal sealed class ExtractToComponentCodeActionProvider(ILoggerFactory loggerFactory) : IRazorCodeActionProvider
{
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<ExtractToComponentCodeActionProvider>();

    public Task<ImmutableArray<RazorVSInternalCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        if (!context.SupportsFileCreation)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        if (!FileKinds.IsComponent(context.CodeDocument.GetFileKind()))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var syntaxTree = context.CodeDocument.GetSyntaxTree();
        if (syntaxTree?.Root is null)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // Make sure the selection starts on an element tag
        var (startElementNode, endElementNode) = GetStartAndEndElements(context, syntaxTree, _logger);
        if (startElementNode is null)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        if (endElementNode is null)
        {
            endElementNode = startElementNode;
        }

        if (!TryGetNamespace(context.CodeDocument, out var @namespace))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var actionParams = CreateInitialActionParams(context, startElementNode, @namespace);        

        if (IsMultiPointSelection(context.Request.Range))
        {
            ProcessMultiPointSelection(startElementNode, endElementNode, actionParams);
        }

        var utilityScanRoot = FindNearestCommonAncestor(startElementNode, endElementNode) ?? startElementNode;
        AddComponentDependenciesInRange(utilityScanRoot,
                                        actionParams.ExtractStart,
                                        actionParams.ExtractEnd,
                                        actionParams);
        GetUsedIdentifiers(utilityScanRoot, syntaxTree.Root, actionParams);

        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            Action = LanguageServerConstants.CodeActions.ExtractToNewComponentAction,
            Language = LanguageServerConstants.CodeActions.Languages.Razor,
            Data = actionParams,
        };

        var codeAction = RazorCodeActionFactory.CreateExtractToComponent(resolutionParams);
        return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([codeAction]);
    }

    private static (MarkupElementSyntax? Start, MarkupElementSyntax? End) GetStartAndEndElements(RazorCodeActionContext context, RazorSyntaxTree syntaxTree, ILogger logger)
    {
        var owner = syntaxTree.Root.FindInnermostNode(context.Location.AbsoluteIndex, includeWhitespace: true);
        if (owner is null)
        {
            logger.LogWarning($"Owner should never be null.");
            return (null, null);
        }

        var startElementNode = owner.FirstAncestorOrSelf<MarkupElementSyntax>();
        if (startElementNode is null || IsInsideProperHtmlContent(context, startElementNode))
        {
            return (null, null);
        }

        var endElementNode = GetEndElementNode(context, syntaxTree, logger);

        return (startElementNode, endElementNode);
    }

    private static bool IsInsideProperHtmlContent(RazorCodeActionContext context, MarkupElementSyntax startElementNode)
    {
        // If the provider executes before the user/completion inserts an end tag, the below return fails
        if (startElementNode.EndTag.IsMissing)
        {
            return true;
        }

        return context.Location.AbsoluteIndex > startElementNode.StartTag.Span.End &&
               context.Location.AbsoluteIndex < startElementNode.EndTag.SpanStart;
    }

    private static MarkupElementSyntax? GetEndElementNode(RazorCodeActionContext context, RazorSyntaxTree syntaxTree)
    {
        var selectionStart = context.Request.Range.Start;
        var selectionEnd = context.Request.Range.End;
        if (selectionStart == selectionEnd)
        {
            return null;
        }

        var endAbsoluteIndex = context.SourceText.GetRequiredAbsoluteIndex(selectionEnd);
        var endOwner = syntaxTree.Root.FindInnermostNode(endAbsoluteIndex, true);
        if (endOwner is null)
        {
            return null;
        }

        // Correct selection to include the current node if the selection ends immediately after a closing tag.
        if (endOwner is MarkupTextLiteralSyntax && endOwner.ContainsOnlyWhitespace() && endOwner.TryGetPreviousSibling(out var previousSibling))
        {
            endOwner = previousSibling;
        }

        return endOwner.FirstAncestorOrSelf<MarkupElementSyntax>();
    }

    private static ExtractToNewComponentCodeActionParams CreateInitialActionParams(RazorCodeActionContext context, MarkupElementSyntax startElementNode, string @namespace)
    {
        return new ExtractToNewComponentCodeActionParams
        {
            Uri = context.Request.TextDocument.Uri,
            ExtractStart = startElementNode.Span.Start,
            ExtractEnd = startElementNode.Span.End,
            Namespace = @namespace,
            Dependencies = [],
            UsedIdentifiers = [],
            UsedMembers = [],
        };
    }

    /// <summary>
    /// Processes a multi-point selection to determine the correct range for extraction.
    /// </summary>
    /// <param name="startElementNode">The starting element of the selection.</param>
    /// <param name="endElementNode">The ending element of the selection, if it exists.</param>
    /// <param name="actionParams">The parameters for the extraction action, which will be updated.</param>
    private static void ProcessSelection(MarkupElementSyntax startElementNode, MarkupElementSyntax? endElementNode, ExtractToNewComponentCodeActionParams actionParams)
    {
        // If there's no end element, we can't process a multi-point selection
        if (endElementNode is null)
        {
            return;
        }

        var startNodeContainsEndNode = endElementNode.Ancestors().Any(node => node == startElementNode);

        // If the start element is an ancestor, keep the original end; otherwise, use the end of the end element
        if (startNodeContainsEndNode)
        {
            actionParams.ExtractEnd = startElementNode.Span.End;
            return;
        }

        // If the start element is not an ancestor of the end element, we need to find a common parent
        // This conditional handles cases where the user's selection spans across different levels of the DOM.
        // For example:
        //   <div>
        //     {|result:<span>
        //      {|selection:<p>Some text</p>
        //     </span>
        //     <span>
        //       <p>More text</p>
        //     </span>
        //     <span>
        //     </span>|}|}
        //   </div>
        // In this case, we need to find the smallest set of complete elements that covers the entire selection.
        
        // Find the closest containing sibling pair that encompasses both the start and end elements
        var (extractStart, extractEnd) = FindContainingSiblingPair(startElementNode, endElementNode);

        // If we found a valid containing pair, update the extraction range
        if (extractStart is not null && extractEnd is not null)
        {
            actionParams.ExtractStart = extractStart.Span.Start;
            actionParams.ExtractEnd = extractEnd.Span.End;
        }
        // Note: If we don't find a valid pair, we keep the original extraction range
    }

    private static bool TryGetNamespace(RazorCodeDocument codeDocument, [NotNullWhen(returnValue: true)] out string? @namespace)
        // If the compiler can't provide a computed namespace it will fallback to "__GeneratedComponent" or
        // similar for the NamespaceNode. This would end up with extracting to a wrong namespace
        // and causing compiler errors. Avoid offering this refactoring if we can't accurately get a
        // good namespace to extract to
        => codeDocument.TryComputeNamespace(fallbackToRootNamespace: true, out @namespace);

    private static (SyntaxNode? Start, SyntaxNode? End) FindContainingSiblingPair(SyntaxNode startNode, SyntaxNode endNode)
    {
        // Find the lowest common ancestor of both nodes
        var nearestCommonAncestor = FindNearestCommonAncestor(startNode, endNode);
        if (nearestCommonAncestor is null)
        {
            return (null, null);
        }

        SyntaxNode? startContainingNode = null;
        SyntaxNode? endContainingNode = null;

        // Pre-calculate the spans for comparison
        var startSpan = startNode.Span;
        var endSpan = endNode.Span;

        foreach (var child in nearestCommonAncestor.ChildNodes().Where(static node => node.Kind == SyntaxKind.MarkupElement))
        {
            var childSpan = child.Span;

            if (startContainingNode is null && childSpan.Contains(startSpan))
            {
                startContainingNode = child;
                if (endContainingNode is not null)
                    break; // Exit if we've found both
            }

            if (childSpan.Contains(endSpan))
            {
                endContainingNode = child;
                if (startContainingNode is not null)
                    break; // Exit if we've found both
            }
        }

        return (startContainingNode, endContainingNode);
    }

    private static SyntaxNode? FindNearestCommonAncestor(SyntaxNode node1, SyntaxNode node2)
    {
        var current = node1;

        while (current is MarkupElementSyntax && current is not null)
        {
            if (current.Span.Contains(node2.Span))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }

    private static void AddComponentDependenciesInRange(SyntaxNode root, int extractStart, int extractEnd, ExtractToNewComponentCodeActionParams actionParams)
    {
        var components = new HashSet<string>();
        var extractSpan = new TextSpan(extractStart, extractEnd - extractStart);

        foreach (var node in root.DescendantNodes())
        {
            if (IsMarkupTagHelperElement(node, extractSpan))
            {
                var tagHelperInfo = GetTagHelperInfo(node);
                if (tagHelperInfo is not null)
                {
                    AddDependenciesFromTagHelperInfo(tagHelperInfo, components, actionParams);
                }
            }
        }
    }

    private static bool IsMarkupTagHelperElement(SyntaxNode node, TextSpan extractSpan)
    {
        return node is MarkupTagHelperElementSyntax markupElement &&
                extractSpan.Contains(markupElement.Span);
    }

    private static TagHelperInfo? GetTagHelperInfo(SyntaxNode node)
    {
        if (node is MarkupTagHelperElementSyntax markupElement)
        {
            return markupElement.TagHelperInfo;
        }

        return null;
    }

    private static void AddDependenciesFromTagHelperInfo(TagHelperInfo tagHelperInfo, HashSet<string> components, ExtractToNewComponentCodeActionParams actionParams)
    {
        foreach (var descriptor in tagHelperInfo.BindingResult.Descriptors)
        {
            if (descriptor is not null)
            {
                foreach (var metadata in descriptor.Metadata)
                {
                    if (metadata.Key == TagHelperMetadata.Common.TypeNamespace &&
                        metadata.Value is not null &&
                        !components.Contains(metadata.Value))
                    {
                        components.Add(metadata.Value);
                        actionParams.Dependencies.Add($"@using {metadata.Value}");
                    }
                }
            }
        }
    }

    private static void GetUsedIdentifiers(SyntaxNode divNode, SyntaxNode documentRoot, ExtractToNewComponentCodeActionParams actionParams)
    {
        HashSet<string> identifiersInScope = [];
        HashSet<string> identifiersInBlock = [];

        HashSet<SyntaxNode> nodesInScope = [];
        HashSet<SyntaxNode> nodesInBlock = [];

        foreach (var node in divNode.DescendantNodes().Where(static node => node.Kind is SyntaxKind.Identifier))
        {
            identifiersInScope.Add(node.GetContent());
            nodesInScope.Add(node);
        }

        foreach (var codeBlock in documentRoot.DescendantNodes().Where(static node => node.Kind is SyntaxKind.RazorDirective))
        {
            foreach (var node in codeBlock.DescendantNodes().Where(static node => node.Kind is SyntaxKind.Identifier))
            {
                identifiersInBlock.Add(node.GetContent());
                nodesInBlock.Add(node);
            }
        }

        nodesInBlock.IntersectWith(nodesInScope);
        identifiersInBlock.IntersectWith(identifiersInScope);
        actionParams.UsedIdentifiers = identifiersInBlock;
    }
}
