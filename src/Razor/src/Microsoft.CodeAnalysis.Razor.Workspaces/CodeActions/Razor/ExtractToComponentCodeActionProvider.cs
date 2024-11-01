﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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

internal sealed class ExtractToComponentCodeActionProvider() : IRazorCodeActionProvider
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

        var actionParams = CreateActionParams(startNode, endNode, @namespace, context);

        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            TextDocument = context.Request.TextDocument,
            Action = LanguageServerConstants.CodeActions.ExtractToNewComponentAction,
            Language = RazorLanguageKind.Razor,
            Data = actionParams,
        };

        var codeAction = RazorCodeActionFactory.CreateExtractToComponent(resolutionParams);
        return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([codeAction]);
    }

    private static (SyntaxNode? Start, SyntaxNode? End) GetStartAndEndElements(RazorCodeActionContext context, RazorSyntaxTree syntaxTree)
    {
        var owner = syntaxTree.Root.FindInnermostNode(context.StartAbsoluteIndex, includeWhitespace: true);
        if (owner is null)
        {
            return (null, null);
        }

        var startElementNode = GetBlockOrTextNode(owner);
        if (startElementNode is null || LocationInvalid(context.StartAbsoluteIndex, startElementNode))
        {
            return (null, null);
        }

        var hasSelection = context.StartAbsoluteIndex != context.EndAbsoluteIndex;

        // In cases where the start element is just a text literal and there
        // is no user selection avoid extracting the whole text literal.
        if (startElementNode is MarkupTextLiteralSyntax && !hasSelection)
        {
            return (null, null);
        }

        var endElementNode = hasSelection
            ? GetEndElementNode(context, syntaxTree)
            : startElementNode;

        return (startElementNode, endElementNode);

        static bool LocationInvalid(int location, SyntaxNode node)
        {
            // Make sure to test for cases where selection
            // is inside of a markup tag such as <p>hello$ there</p>
            if (node is MarkupElementSyntax markupElement)
            {
                return location > markupElement.StartTag.Span.End &&
                        location < markupElement.EndTag.SpanStart;
            }

            return !node.Span.Contains(location);
        }
    }

    private static SyntaxNode? GetEndElementNode(RazorCodeActionContext context, RazorSyntaxTree syntaxTree)
    {
        var endOwner = syntaxTree.Root.FindInnermostNode(context.EndAbsoluteIndex, includeWhitespace: true);
        if (endOwner is null)
        {
            return null;
        }

        // Correct selection to include the current node if the selection ends immediately after a closing tag.
        if (endOwner is MarkupTextLiteralSyntax markupTextLiteral
            && SelectionShouldBePrevious(markupTextLiteral, context.EndAbsoluteIndex)
            && endOwner.TryGetPreviousSibling(out var previousSibling))
        {
            endOwner = previousSibling;
        }

        return GetBlockOrTextNode(endOwner);

        static bool SelectionShouldBePrevious(MarkupTextLiteralSyntax markupTextLiteral, int absoluteIndex)
            => markupTextLiteral.Span.Start == absoluteIndex
                || markupTextLiteral.ContainsOnlyWhitespace();
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

    private static bool IsBlockNode(SyntaxNode node)
        => node.Kind is
                SyntaxKind.MarkupElement or
                SyntaxKind.MarkupTagHelperElement or
                SyntaxKind.CSharpCodeBlock;

    private static ExtractToComponentCodeActionParams CreateActionParams(
        SyntaxNode startNode,
        SyntaxNode endNode,
        string @namespace,
        RazorCodeActionContext context)
    {
        
        var selectionSpan = AreSiblings(startNode, endNode)
            ? TextSpan.FromBounds(startNode.Span.Start, endNode.Span.End)
            : GetEncompassingTextSpan(startNode, endNode);

        if (startNode is MarkupTextLiteralSyntax)
        {
            selectionSpan = TextSpan.FromBounds(context.StartAbsoluteIndex, selectionSpan.End);
        }

        if (endNode is MarkupTextLiteralSyntax)
        {
            selectionSpan = TextSpan.FromBounds(selectionSpan.Start, context.EndAbsoluteIndex);
        }

        return new ExtractToComponentCodeActionParams
        {
            Start = selectionSpan.Start,
            End = selectionSpan.End,
            Namespace = @namespace
        };
    }

    private static TextSpan GetEncompassingTextSpan(SyntaxNode startNode, SyntaxNode endNode)
    {
        // Find a valid node that encompasses both the start and the end to
        // become the selection.
        var commonAncestor = endNode.Span.Contains(startNode.Span)
            ? endNode
            : startNode;

        while (commonAncestor is MarkupElementSyntax or
                MarkupTagHelperAttributeSyntax or
                MarkupBlockSyntax)
        {
            if (commonAncestor.Span.Contains(startNode.Span) &&
                commonAncestor.Span.Contains(endNode.Span))
            {
                break;
            }

            commonAncestor = commonAncestor.Parent;
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
            foreach (var child in commonAncestor.ChildNodes().Where(static node => node.Kind == SyntaxKind.MarkupElement))
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
}
