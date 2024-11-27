﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.CodeActions.Razor;

using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

internal class ExtractToComponentCodeActionProvider() : IRazorCodeActionProvider
{
    public Task<ImmutableArray<RazorVSInternalCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
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

        if (!TryGetNamespace(context.CodeDocument, out var @namespace))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var (startNode, endNode) = GetStartAndEndElements(context, syntaxTree);
        if (startNode is null || endNode is null)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        // If the selection begins in @code don't offer to extract. The inserted
        // component would not be valid since it's inserted at the starting point
        if (RazorSyntaxFacts.IsInCodeBlock(startNode))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var possibleSpan = TryGetSpanFromNodes(startNode, endNode, context);
        if (possibleSpan is not { } span)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var actionParams = new ExtractToComponentCodeActionParams
        {
            Start = span.Start,
            End = span.End,
            Namespace = @namespace
        };

        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            TextDocument = context.Request.TextDocument,
            Action = LanguageServerConstants.CodeActions.ExtractToNewComponentAction,
            Language = RazorLanguageKind.Razor,
            DelegatedDocumentUri = context.DelegatedDocumentUri,
            Data = actionParams,
        };

        var codeAction = RazorCodeActionFactory.CreateExtractToComponent(resolutionParams);
        return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([codeAction]);
    }

    private static (SyntaxNode? Start, SyntaxNode? End) GetStartAndEndElements(RazorCodeActionContext context, RazorSyntaxTree syntaxTree)
    {
        var owner = syntaxTree.Root.FindInnermostNode(context.StartAbsoluteIndex, includeWhitespace: !context.HasSelection);
        if (owner is null)
        {
            return (null, null);
        }

        // In cases where the start element is just a text literal and there
        // is no user selection avoid extracting the whole text literal.or
        // the parent element.
        if (owner is MarkupTextLiteralSyntax && !context.HasSelection)
        {
            return (null, null);
        }

        var startElementNode = GetBlockOrTextNode(owner);
        if (startElementNode is null)
        {
            return (null, null);
        }

        var endElementNode = context.HasSelection
            ? GetEndElementNode(context, syntaxTree)
            : startElementNode;

        return (startElementNode, endElementNode);
    }

    private static SyntaxNode? GetEndElementNode(RazorCodeActionContext context, RazorSyntaxTree syntaxTree)
    {
        var endOwner = syntaxTree.Root.FindInnermostNode(context.EndAbsoluteIndex, includeWhitespace: false);
        if (endOwner is null)
        {
            return null;
        }

        return GetBlockOrTextNode(endOwner);
    }

    private static SyntaxNode? GetBlockOrTextNode(SyntaxNode node)
    {
        var blockNode = node.FirstAncestorOrSelf<SyntaxNode>(IsBlockNode);
        if (blockNode is not null)
        {
            return blockNode;
        }

        // Account for cases where a text literal is not contained
        // within a block node. For example:
        // <h1> Example </h1>
        // [|This is not in a block but is a valid selection|]
        if (node is MarkupTextLiteralSyntax markupTextLiteral)
        {
            return markupTextLiteral;
        }

        return null;
    }

    private TextSpan? TryGetSpanFromNodes(SyntaxNode startNode, SyntaxNode endNode, RazorCodeActionContext context)
    {
        // First get a decent span to work with. If the two nodes chosen
        // are siblings (even with elements in between) then their start/end
        // work fine. However, if the two nodes are not siblings then
        // some work has to be done. See GetEncompassingTextSpan for the
        // information on the heuristic for choosing a span in that case.
        var initialSpan = AreSiblings(startNode, endNode)
            ? TextSpan.FromBounds(startNode.Span.Start, endNode.Span.End)
            : GetEncompassingTextSpan(startNode, endNode);

        if (initialSpan is not { } selectionSpan)
        {
            return null;
        }

        // Now that a span is chosen there is still a chance the user intended only
        // part of text to be chosen. If the start or end node are text AND the selection span
        // is inside those nodes modify the selection to be the users initial point. That makes sure
        // all the text isn't included and only the user selected text is.
        // NOTE: Intersects with is important because we want to include the end position when comparing.
        if (startNode is MarkupTextLiteralSyntax && startNode.Span.IntersectsWith(selectionSpan.Start))
        {
            selectionSpan = TextSpan.FromBounds(context.StartAbsoluteIndex, selectionSpan.End);
        }

        if (endNode is MarkupTextLiteralSyntax && endNode.Span.IntersectsWith(selectionSpan.End))
        {
            selectionSpan = TextSpan.FromBounds(selectionSpan.Start, context.EndAbsoluteIndex);
        }

        return selectionSpan;
    }

    private static TextSpan? GetEncompassingTextSpan(SyntaxNode startNode, SyntaxNode endNode)
    {
        // Find a valid node that encompasses both the start and the end to
        // become the selection.
        var commonAncestor = endNode.Span.Contains(startNode.Span)
            ? endNode
            : startNode;

        // IsBlockOrMarkupBlockNode because the common ancestor could be a MarkupBlock
        // even if that's an invalid start/end node.
        commonAncestor = commonAncestor.FirstAncestorOrSelf<SyntaxNode>(IsBlockOrMarkupBlockNode);
        while (commonAncestor is not null && IsBlockOrMarkupBlockNode(commonAncestor))
        {
            if (commonAncestor.Span.Contains(startNode.Span) &&
                commonAncestor.Span.Contains(endNode.Span))
            {
                break;
            }

            commonAncestor = commonAncestor.Parent;
        }

        if (commonAncestor is null)
        {
            return null;
        }

        // If walking up the tree was required then make sure to reduce
        // selection back down to minimal nodes needed.
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
        if (commonAncestor != startNode &&
            commonAncestor != endNode)
        {
            SyntaxNode? modifiedStart = null, modifiedEnd = null;
            foreach (var child in commonAncestor.ChildNodes())
            {
                if (child.Span.Contains(startNode.Span))
                {
                    modifiedStart = child;
                    if (modifiedEnd is not null)
                        break; // Exit if we've found both
                }

                if (child.Span.Contains(endNode.Span))
                {
                    modifiedEnd = child;
                    if (modifiedStart is not null)
                        break; // Exit if we've found both
                }
            }

            // There's a start and end node that are siblings and will work for start/end
            // of extraction into the new component.
            if (modifiedStart is not null && modifiedEnd is not null)
            {
                return TextSpan.FromBounds(modifiedStart.Span.Start, modifiedEnd.Span.End);
            }
        }

        // Fallback to extracting the nearest common ancestor span
        return commonAncestor.Span;
    }

    private static bool AreSiblings(SyntaxNode? node1, SyntaxNode? node2)
    {
        if (node1 is null)
        {
            return false;
        }

        if (node2 is null)
        {
            return false;
        }

        return node1.Parent == node2.Parent;
    }

    private static bool TryGetNamespace(RazorCodeDocument codeDocument, [NotNullWhen(returnValue: true)] out string? @namespace)
        // If the compiler can't provide a computed namespace it will fallback to "__GeneratedComponent" or
        // similar for the NamespaceNode. This would end up with extracting to a wrong namespace
        // and causing compiler errors. Avoid offering this refactoring if we can't accurately get a
        // good namespace to extract to
        => codeDocument.TryComputeNamespace(fallbackToRootNamespace: true, out @namespace);

    private static bool IsBlockNode(SyntaxNode node)
        => node.Kind is
                SyntaxKind.MarkupElement or
                SyntaxKind.MarkupTagHelperElement or
                SyntaxKind.CSharpCodeBlock;

    private static bool IsBlockOrMarkupBlockNode(SyntaxNode node)
        => IsBlockNode(node)
            || node.Kind == SyntaxKind.MarkupBlock;
}
