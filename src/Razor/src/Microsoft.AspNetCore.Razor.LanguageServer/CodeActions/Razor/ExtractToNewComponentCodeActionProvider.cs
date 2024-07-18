// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;

internal sealed class ExtractToNewComponentCodeActionProvider : IRazorCodeActionProvider
{
    private readonly ILogger _logger;

    public ExtractToNewComponentCodeActionProvider(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.GetOrCreateLogger<ExtractToNewComponentCodeActionProvider>();
    }

    public Task<IReadOnlyList<RazorVSInternalCodeAction>?> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        if (!context.SupportsFileCreation)
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        if (!FileKinds.IsComponent(context.CodeDocument.GetFileKind()))
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        var syntaxTree = context.CodeDocument.GetSyntaxTree();
        if (syntaxTree?.Root is null)
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        var startOwner = syntaxTree.Root.FindInnermostNode(context.Location.AbsoluteIndex, true);
        if (startOwner is null)
        {
            _logger.LogWarning($"Owner should never be null.");
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        var startComponentNode = startOwner.FirstAncestorOrSelf<MarkupElementSyntax>();

        var selectionStart = context.Request.Range.Start;
        var selectionEnd = context.Request.Range.End;
        var isSelection = selectionStart != selectionEnd;

        // If user selects range from end to beginning (i.e., bottom-to-top or right-to-left), get the effective start and end.
        if (selectionEnd is not null && selectionEnd.Line < selectionStart.Line ||
           (selectionEnd is not null && selectionEnd.Line == selectionStart.Line && selectionEnd.Character < selectionStart.Character))
        {
            (selectionEnd, selectionStart) = (selectionStart, selectionEnd);
        }

        var selectionEndIndex = new SourceLocation(0, 0, 0);
        var endOwner = startOwner;
        var endComponentNode = startComponentNode;

        string specialEndToken;

        if (isSelection)
        {
            if (!selectionEnd.TryGetSourceLocation(context.CodeDocument.GetSourceText(), _logger, out var location))
            {
                return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
            }
     
            if (location is null)
            {
                return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
            }

            selectionEndIndex = location.Value;
            endOwner = syntaxTree.Root.FindInnermostNode(selectionEndIndex.AbsoluteIndex, true);

            if (endOwner is null)
            {
                return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
            }

            endComponentNode = endOwner.FirstAncestorOrSelf<MarkupElementSyntax>();

            // When range ends immediately after a closing tag, the subsequent node is selected. This ensures the current node is selected.
            if (endOwner.ToFullString().Trim().IsNullOrEmpty())
            {
                endOwner.TryGetPreviousSibling(out endOwner);
                endComponentNode = endOwner.FirstAncestorOrSelf<MarkupElementSyntax>();

            }
        }

        // Make sure we've found tag
        if (startComponentNode is null)
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        // Do not provide code action if the cursor is inside proper html content (i.e. rendered text)
        if (context.Location.AbsoluteIndex > startComponentNode.StartTag.Span.End &&
            context.Location.AbsoluteIndex < startComponentNode.EndTag.SpanStart)
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        if (!TryGetNamespace(context.CodeDocument, out var @namespace))
        {
            return SpecializedTasks.Null<IReadOnlyList<RazorVSInternalCodeAction>>();
        }

        var actionParams = new ExtractToNewComponentCodeActionParams()
        {
            Uri = context.Request.TextDocument.Uri,
            ExtractStart = startComponentNode.Span.Start,
            ExtractEnd = startComponentNode.Span.End,
            Namespace = @namespace,
            Dependencies = new List<string>()
        };

        bool selectionStartHasParentElement = false;
        SyntaxNode? extractStart = null;
        SyntaxNode? extractEnd = null;

        if (isSelection && endComponentNode is not null)
        {
            // If component @ start of the selection includes a parent element of the component @ end of selection, then proceed as usual.
            // If not, limit extraction to end of component @ end of selection (in the simplest case)
            selectionStartHasParentElement = endComponentNode.Ancestors().Any(node => node == startComponentNode);
            actionParams.ExtractEnd = selectionStartHasParentElement ? actionParams.ExtractEnd : endComponentNode.Span.End;

            // Handle other case: Either start/end of selection is nested within a component
            if (!selectionStartHasParentElement)
            {
                (extractStart, extractEnd) = FindContainingSiblingPair(startComponentNode, endComponentNode);
                if (extractStart != null && extractEnd != null)
                {
                    actionParams.ExtractStart = extractStart.Span.Start;
                    actionParams.ExtractEnd = extractEnd.Span.End;
                }
            }
        }

        var componentsInRange = IdentifyComponentsInRange(FindLowestCommonAncestor(startComponentNode, endComponentNode),
                                                          actionParams.ExtractStart, actionParams.ExtractEnd);
        if (componentsInRange.Count > 0)
            actionParams.Dependencies = GetAllUsingStatements(syntaxTree.Root);

        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            Action = LanguageServerConstants.CodeActions.ExtractToNewComponentAction,
            Language = LanguageServerConstants.CodeActions.Languages.Razor,
            Data = actionParams,
        };

        var codeAction = RazorCodeActionFactory.CreateExtractToNewComponent(resolutionParams);
        var codeActions = new List<RazorVSInternalCodeAction> { codeAction };

        return Task.FromResult<IReadOnlyList<RazorVSInternalCodeAction>?>(codeActions);
    }

    private static bool TryGetNamespace(RazorCodeDocument codeDocument, [NotNullWhen(returnValue: true)] out string? @namespace)
        // If the compiler can't provide a computed namespace it will fallback to "__GeneratedComponent" or
        // similar for the NamespaceNode. This would end up with extracting to a wrong namespace
        // and causing compiler errors. Avoid offering this refactoring if we can't accurately get a
        // good namespace to extract to
        => codeDocument.TryComputeNamespace(fallbackToRootNamespace: true, out @namespace);

    private static (SyntaxNode Start, SyntaxNode End) FindContainingSiblingPair(SyntaxNode startNode, SyntaxNode endNode)
    {
        // Find the lowest common ancestor of both nodes
        var lowestCommonAncestor = FindLowestCommonAncestor(startNode, endNode);
        if (lowestCommonAncestor == null)
        {
            return (null, null);
        }

        SyntaxNode startContainingNode = null;
        SyntaxNode endContainingNode = null;

        // Pre-calculate the spans for comparison
        var startSpan = startNode.Span;
        var endSpan = endNode.Span;


        foreach (var child in lowestCommonAncestor.ChildNodes().Where(node => node.Kind == SyntaxKind.MarkupElement))
        {
            var childSpan = child.Span;

            if (startContainingNode == null && childSpan.Contains(startSpan))
            {
                startContainingNode = child;
                if (endContainingNode != null) break; // Exit if we've found both
            }

            if (childSpan.Contains(endSpan))
            {
                endContainingNode = child;
                if (startContainingNode != null) break; // Exit if we've found both
            }
        }

        return (startContainingNode, endContainingNode);
    }

    private static SyntaxNode? FindLowestCommonAncestor(SyntaxNode node1, SyntaxNode node2)
    {
        var current = node1;

        while (current.Kind == SyntaxKind.MarkupElement && current != null)
        {
            if (current.Span.Contains(node2.Span))
            {
                return current;
            }
            current = current.Parent;
        }

        return null;
    }
    private static HashSet<SyntaxNode> IdentifyComponentsInRange(SyntaxNode root, int extractStart, int extractEnd)
    {
        var components = new HashSet<SyntaxNode>();
        var extractSpan = new TextSpan(extractStart, extractEnd - extractStart);

        foreach (var node in root.DescendantNodes())
        {
            if (node is MarkupTagHelperElementSyntax markupElement &&
                extractSpan.Contains(markupElement.Span))
            {
                components.Add(markupElement);
            }
        }

        return components;
    }

    private static List<string> GetAllUsingStatements(SyntaxNode root)
    {
        return root.DescendantNodes()
                   .OfType<CSharpCodeBlockSyntax>()
                   .SelectMany(cSharpBlock => cSharpBlock.ChildNodes())
                   .OfType<RazorDirectiveSyntax>()
                   .Select(directive => directive.ToFullString().TrimStart())
                   .Where(directiveString => directiveString.StartsWith("@using"))
                   .Select(directiveString => directiveString.Trim())
                   .ToList();
    }

    //private static bool HasUnsupportedChildren(Language.Syntax.SyntaxNode node)
    //{
    //    return node.DescendantNodes().Any(static n => n is MarkupBlockSyntax or CSharpTransitionSyntax or RazorCommentBlockSyntax);
    //}
}
