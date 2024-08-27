// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

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

        ProcessSelection(startElementNode, endElementNode, actionParams);

        var utilityScanRoot = FindNearestCommonAncestor(startElementNode, endElementNode) ?? startElementNode;

        // The new component usings are going to be a subset of the usings in the source razor file.
        var usingStrings = syntaxTree.Root.DescendantNodes().Where(node => node.IsUsingDirective(out var _)).Select(node => node.ToFullString().TrimEnd());

        // Get only the namespace after the "using" keyword.
        var usingNamespaceStrings = usingStrings.Select(usingString => usingString.Substring("using  ".Length));

        AddUsingDirectivesInRange(utilityScanRoot,
                                  usingNamespaceStrings,
                                  actionParams.ExtractStart,
                                  actionParams.ExtractEnd,
                                  actionParams);

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

        var endElementNode = GetEndElementNode(context, syntaxTree);

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

    private static ExtractToComponentCodeActionParams CreateInitialActionParams(RazorCodeActionContext context, MarkupElementSyntax startElementNode, string @namespace)
    {
        return new ExtractToComponentCodeActionParams
        {
            Uri = context.Request.TextDocument.Uri,
            ExtractStart = startElementNode.Span.Start,
            ExtractEnd = startElementNode.Span.End,
            Namespace = @namespace,
            usingDirectives = []
        };
    }

    /// <summary>
    /// Processes a multi-point selection to determine the correct range for extraction.
    /// </summary>
    /// <param name="startElementNode">The starting element of the selection.</param>
    /// <param name="endElementNode">The ending element of the selection, if it exists.</param>
    /// <param name="actionParams">The parameters for the extraction action, which will be updated.</param>
    private static void ProcessSelection(MarkupElementSyntax startElementNode, MarkupElementSyntax? endElementNode, ExtractToComponentCodeActionParams actionParams)
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

        while (current is MarkupElementSyntax or
                MarkupTagHelperAttributeSyntax or
                MarkupBlockSyntax &&
            current is not null)
        {
            if (current.Span.Contains(node2.Span))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }

    private static void AddUsingDirectivesInRange(SyntaxNode root, IEnumerable<string> usingsInSourceRazor, int extractStart, int extractEnd, ExtractToComponentCodeActionParams actionParams)
    {
        var components = new HashSet<string>();
        var extractSpan = new TextSpan(extractStart, extractEnd - extractStart);

        foreach (var node in root.DescendantNodes().Where(node => extractSpan.Contains(node.Span)))
        {
            if (node is MarkupTagHelperElementSyntax { TagHelperInfo: { } tagHelperInfo })
            {
                AddUsingFromTagHelperInfo(tagHelperInfo, components, usingsInSourceRazor, actionParams);
            }
        }
    }

    private static void AddUsingFromTagHelperInfo(TagHelperInfo tagHelperInfo, HashSet<string> components, IEnumerable<string> usingsInSourceRazor, ExtractToComponentCodeActionParams actionParams)
    {
        foreach (var descriptor in tagHelperInfo.BindingResult.Descriptors)
        {
            if (descriptor is null)
            {
                continue;
            }

            var typeNamespace = descriptor.GetTypeNamespace();

            // Since the using directive at the top of the file may be relative and not absolute,
            // we need to generate all possible partial namespaces from `typeNamespace`.

            // Potentially, the alternative could be to ask if the using namespace at the top is a substring of `typeNamespace`.
            // The only potential edge case is if there are very similar namespaces where one
            // is a substring of the other, but they're actually different (e.g., "My.App" and "My.Apple").

            // Generate all possible partial namespaces from `typeNamespace`, from least to most specific
            // (assuming that the user writes absolute `using` namespaces most of the time)

            // This is a bit inefficient because at most 'k' string operations are performed (k = parts in the namespace),
            // for each potential using directive.

            var parts = typeNamespace.Split('.');
            for (var i = 0; i < parts.Length; i++)
            {
                var partialNamespace = string.Join(".", parts.Skip(i));

                if (components.Add(partialNamespace) && usingsInSourceRazor.Contains(partialNamespace))
                {
                    actionParams.usingDirectives.Add($"@using {partialNamespace}");
                    break;
                }
            }
        }
    }
}
