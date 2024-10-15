// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.Decompiler.CSharp.Syntax;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;

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

        var actionParams = CreateActionParams(context, startNode, endNode, @namespace);

        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            Action = LanguageServerConstants.CodeActions.ExtractToNewComponentAction,
            Language = LanguageServerConstants.CodeActions.Languages.Razor,
            Data = actionParams,
        };

        var codeAction = RazorCodeActionFactory.CreateExtractToComponent(resolutionParams);
        return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([codeAction]);
    }

    private static (MarkupElementSyntax? Start, MarkupElementSyntax? End) GetStartAndEndElements(RazorCodeActionContext context, RazorSyntaxTree syntaxTree)
    {
        var owner = syntaxTree.Root.FindInnermostNode(context.StartLocation.AbsoluteIndex, includeWhitespace: true);
        if (owner is null)
        {
            return (null, null);
        }

        var startElementNode = owner.FirstAncestorOrSelf<MarkupElementSyntax>();
        if (startElementNode is null || LocationOutsideNode(context.StartLocation, startElementNode))
        {
            return (null, null);
        }

        var endElementNode = context.StartLocation == context.EndLocation
            ? startElementNode
            : GetEndElementNode(context, syntaxTree);

        return (startElementNode, endElementNode);
    }

    private static bool LocationOutsideNode(SourceLocation location, MarkupElementSyntax node)
    {
        return location.AbsoluteIndex > node.StartTag.Span.End &&
               location.AbsoluteIndex < node.EndTag.SpanStart;
    }

    private static MarkupElementSyntax? GetEndElementNode(RazorCodeActionContext context, RazorSyntaxTree syntaxTree)
    {
        var endOwner = syntaxTree.Root.FindInnermostNode(context.EndLocation.AbsoluteIndex, includeWhitespace: true);
        if (endOwner is null)
        {
            return null;
        }

        // Correct selection to include the current node if the selection ends immediately after a closing tag.
        if (endOwner is MarkupTextLiteralSyntax
            && endOwner.ContainsOnlyWhitespace()
            && endOwner.TryGetPreviousSibling(out var previousSibling))
        {
            endOwner = previousSibling;
        }

        return endOwner.FirstAncestorOrSelf<MarkupElementSyntax>();
    }

    private static ExtractToComponentCodeActionParams CreateActionParams(
        RazorCodeActionContext context,
        MarkupElementSyntax startNode,
        MarkupElementSyntax endNode,
        string @namespace)
    {
        var selectionSpan = AreSiblings(startNode, endNode)
            ? TextSpan.FromBounds(startNode.Span.Start, endNode.Span.End)
            : GetEncompassingTextSpan(startNode, endNode);

        return new ExtractToComponentCodeActionParams
        {
            Uri = context.Request.TextDocument.Uri,
            Start = selectionSpan.Start,
            End = selectionSpan.End,
            Namespace = @namespace
        };
    }

    private static TextSpan GetEncompassingTextSpan(MarkupElementSyntax startNode, MarkupElementSyntax endNode)
    {
        // Find a valid node that encompasses both the start and the end to
        // become the selection.
        SyntaxNode commonAncestor = endNode.Span.Contains(startNode.Span)
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

    private static bool AreSiblings(SyntaxNode node1, SyntaxNode node2)
        => node1.Parent == node2.Parent;

    private static bool TryGetNamespace(RazorCodeDocument codeDocument, [NotNullWhen(returnValue: true)] out string? @namespace)
        // If the compiler can't provide a computed namespace it will fallback to "__GeneratedComponent" or
        // similar for the NamespaceNode. This would end up with extracting to a wrong namespace
        // and causing compiler errors. Avoid offering this refactoring if we can't accurately get a
        // good namespace to extract to
        => codeDocument.TryComputeNamespace(fallbackToRootNamespace: true, out @namespace);
}
