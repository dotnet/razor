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
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.AspNetCore.Razor.Threading;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;

internal sealed class ExtractToComponentCodeActionProvider(ILoggerFactory loggerFactory, ITelemetryReporter telemetryReporter) : IRazorCodeActionProvider
{
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<ExtractToComponentCodeActionProvider>();
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;

    public Task<ImmutableArray<RazorVSInternalCodeAction>> ProvideAsync(RazorCodeActionContext context, CancellationToken cancellationToken)
    {
        var telemetryDidSucceed = false;
        using var _ = _telemetryReporter.BeginBlock("extractToComponentProvider", Severity.Normal, new Property("didSucceed", telemetryDidSucceed));

        if (!IsValidContext(context))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        if (!TryGetNamespace(context.CodeDocument, out var @namespace))
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var selectionAnalysisResult = TryAnalyzeSelection(context);
        if (!selectionAnalysisResult.Success)
        {
            return SpecializedTasks.EmptyImmutableArray<RazorVSInternalCodeAction>();
        }

        var actionParams = new ExtractToComponentCodeActionParams
        {
            Uri = context.Request.TextDocument.Uri,
            ExtractStart = selectionAnalysisResult.ExtractStart,
            ExtractEnd = selectionAnalysisResult.ExtractEnd,
            HasEventHandlerOrExpression = selectionAnalysisResult.HasEventHandlerOrExpression,
            HasAtCodeBlock = selectionAnalysisResult.HasAtCodeBlock,
            UsingDirectives = selectionAnalysisResult.UsingDirectives ?? Array.Empty<string>(),
            DedentWhitespaceString = selectionAnalysisResult.DedentWhitespaceString ?? string.Empty,
            Namespace = @namespace,
        };

        var resolutionParams = new RazorCodeActionResolutionParams()
        {
            Action = LanguageServerConstants.CodeActions.ExtractToComponentAction,
            Language = LanguageServerConstants.CodeActions.Languages.Razor,
            Data = actionParams,
        };

        var codeAction = RazorCodeActionFactory.CreateExtractToComponent(resolutionParams);

        telemetryDidSucceed = true;
        return Task.FromResult<ImmutableArray<RazorVSInternalCodeAction>>([codeAction]);
    }

    private static bool IsValidContext(RazorCodeActionContext context)
    {
        return context is not null &&
               context.SupportsFileCreation &&
               FileKinds.IsComponent(context.CodeDocument.GetFileKind()) &&
               !context.CodeDocument.IsUnsupported() &&
               context.CodeDocument.GetSyntaxTree() is not null;
    }

    internal sealed record SelectionAnalysisResult
    {
        public required bool Success;
        public int ExtractStart;
        public int ExtractEnd;
        public bool HasAtCodeBlock;
        public bool HasEventHandlerOrExpression;
        public string[]? UsingDirectives;
        public string? DedentWhitespaceString;
    }

    private SelectionAnalysisResult TryAnalyzeSelection(RazorCodeActionContext context)
    {
        var treeRoot = context.CodeDocument.GetSyntaxTree().Root;
        var sourceText = context.SourceText;

        var startAbsoluteIndex = context.Location.AbsoluteIndex;
        var endAbsoluteIndex = sourceText.GetRequiredAbsoluteIndex(context.Request.Range.End);

        var startOwner = treeRoot.FindInnermostNode(startAbsoluteIndex, includeWhitespace: true);
        var endOwner = treeRoot.FindInnermostNode(endAbsoluteIndex, includeWhitespace: true);

        if (startOwner is null || endOwner is null)
        {
            _logger.LogWarning($"Owner should never be null.");
            return new SelectionAnalysisResult { Success = false };
        }

        (startOwner, var startElementNode, var hasAtCodeBlock) = AnalyzeSelectionStart(startOwner);
        (endOwner, var endElementNode, hasAtCodeBlock) = AnalyzeSelectionEnd(endOwner);

        // At this point, at least one end of the selection must either be a valid `MarkupElement` or `MarkupTagHelperElement`
        var isValidStartElementNode = startElementNode is not null &&
            !startElementNode.GetDiagnostics().Any(d => d.Severity == RazorDiagnosticSeverity.Error);

        var isValidEndElementNode = endElementNode is not null &&
            !endElementNode.GetDiagnostics().Any(d => d.Severity == RazorDiagnosticSeverity.Error);

        var selectionEndsAreValid = IsOnMarkupTag(startAbsoluteIndex, startOwner) || IsOnMarkupTag(endAbsoluteIndex, endOwner);

        if (!selectionEndsAreValid || !(isValidStartElementNode || isValidEndElementNode))
        {
            return new SelectionAnalysisResult { Success = false };
        }

        // Use element nodes if found, otherwise use the original owners (either could be CSHarpCodeBlockSyntax)
        startOwner = startElementNode is not null ? startElementNode : startOwner;
        endOwner = endElementNode is not null ? endElementNode : endOwner;

        // Process the selection to determine exact extraction bounds
        // Note: startOwner and endOwner are modified in-place to correct for sibling selection and adjacent scenarios, if necessary
        if (!TryProcessSelection(ref startOwner, ref endOwner))
        {
            return new SelectionAnalysisResult { Success = false };
        }

        // Check if there are any components inside the selection that require using statements in the new component.
        // In all scenarios, @usings for a new component are a subset of @usings in the source file.
        var scanRoot = FindNearestCommonAncestor(startOwner, endOwner) ?? treeRoot; // Fallback to the tree root if no common ancestor is found

        int extractStart = startOwner.Span.Start, extractEnd = endOwner.Span.End;
        // Also check for event handler and data binding identifiers.
        var (hasOtherIdentifiers, usingDirectivesInRange) = GetUsingsIdentifiersInRange(treeRoot, scanRoot, extractStart, extractEnd);

        // Get dedent whitespace
        // The amount of whitespace to dedent is the smaller of the whitespaces before the start element and before the end MarkupElement if they exist.
        // Another way to think about it is that we want to dedent the selection to the smallest, nonempty common whitespace prefix, if it exists.

        // For example:
        // <div id="parent">
        //      <div>
        //          <[|p>Some text</p>
        //      </div>
        // </div|]>
        // The dedent whitespace should be based on the end MarkupElement (</div>), not the start MarkupElement (<p>).
        var dedentWhitespace = string.Empty;
        if (isValidStartElementNode &&
            startOwner?.TryGetPreviousSibling(out var whitespaceNode) == true &&
            whitespaceNode.ContainsOnlyWhitespace())
        {
            var startWhitespace = whitespaceNode.ToFullString();
            startWhitespace = startWhitespace.Replace("\r", string.Empty).Replace("\n", string.Empty);

            if (!startWhitespace.IsNullOrEmpty())
            {
                dedentWhitespace = startWhitespace;
            }
        }

        if (isValidEndElementNode &&
            endOwner?.TryGetPreviousSibling(out whitespaceNode) == true &&
            whitespaceNode.ContainsOnlyWhitespace())
        {
            var endDedentWhitespace = whitespaceNode.ToFullString();
            endDedentWhitespace = endDedentWhitespace.Replace("\r", string.Empty).Replace("\n", string.Empty);

            if (!endDedentWhitespace.IsNullOrEmpty() &&
                    (dedentWhitespace.IsNullOrEmpty() ||
                    endDedentWhitespace.Length < dedentWhitespace.Length))
            {
                dedentWhitespace = endDedentWhitespace;
            }
        }

        return new SelectionAnalysisResult
        {
            Success = true,
            ExtractStart = extractStart,
            ExtractEnd = extractEnd,
            HasAtCodeBlock = hasAtCodeBlock,
            HasEventHandlerOrExpression = hasOtherIdentifiers,
            UsingDirectives = usingDirectivesInRange.ToArray(),
            DedentWhitespaceString = dedentWhitespace
        };
    }

    private static (SyntaxNode owner, MarkupSyntaxNode? startElementNode, bool hasAtCodeBlock) AnalyzeSelectionStart(SyntaxNode startOwner)
    {
        var elementNode = startOwner.FirstAncestorOrSelf<MarkupSyntaxNode>(node => node is MarkupElementSyntax or MarkupTagHelperElementSyntax);
        var hasAtCodeBlock = false;

        if (elementNode is null)
        {
            var codeBlock = startOwner.FirstAncestorOrSelf<CSharpCodeBlockSyntax>();
            if (codeBlock is not null)
            {
                hasAtCodeBlock = true;
                startOwner = codeBlock;
            }
        }

        return (startOwner, elementNode, hasAtCodeBlock);
    }

    private static (SyntaxNode owner, MarkupSyntaxNode? endElementNode, bool hasAtCodeBlock) AnalyzeSelectionEnd(SyntaxNode endOwner)
     {
        var elementNode = endOwner.FirstAncestorOrSelf<MarkupSyntaxNode>(node => node is MarkupElementSyntax or MarkupTagHelperElementSyntax);
        var hasAtCodeBlock = false;

        // Case 1: Selection ends at the "edge" of a tag (i.e. immediately after the ">")
        if (endOwner is MarkupTextLiteralSyntax && endOwner.ContainsOnlyWhitespace() && endOwner.TryGetPreviousSibling(out var previousMarkupSibling))
        {
            elementNode = previousMarkupSibling.FirstAncestorOrSelf<MarkupSyntaxNode>(node => node is MarkupElementSyntax or MarkupTagHelperElementSyntax);
        }

        // Case 2: Selection ends at the end of a code block (i.e. immediately after the "}")
        if (ShouldAdjustEndNode(endOwner, elementNode))
        {
            var adjustedNode = AdjustEndNode(endOwner);
            if (adjustedNode is CSharpCodeBlockSyntax)
            {
                hasAtCodeBlock = true;
                endOwner = adjustedNode;
            }
        }

        return (endOwner, elementNode, hasAtCodeBlock);
    }

    private static bool ShouldAdjustEndNode(SyntaxNode endOwner, MarkupSyntaxNode? elementNode)
    // When the user selects a code block and the selection ends immediately after the right brace:
    // If there is no more text content after the right brace, 'endOwner' will be a MarkupTextLiteral with a Marker token inside.
    // If there is content after the right brace (including even a NewLine), 'endOwner' will be a 'RazorMetaCode' with a NewLine token.

    // If `endOwner` is `MarkupTextLiteral`, its previous sibling will be the `CSharpCodeBlock` itself.
    // MarkupBlock -> (CSharpCodeBlock, MarkupTextLiteral -> Marker)

    // If `endOwner` is 'RazorMetaCode`, its previous sibling will be the `RazorDirective` immediately inside `CSharpCodeBlock`.
    // MarkupBlock -> CSharpCodeBlock -> (RazorDirective, RazorMetaCode)

    // In both cases above, the desired end node is the `CSharpCodeBlock` itself.
    // For the first case, it's previous sibling of `MarkupTextLiteral`
    // For the second case, it's the parent of both 'RazorDirective' and its previous sibling.
    => elementNode is null && (
        (endOwner is MarkupTextLiteralSyntax textLiteral && textLiteral.LiteralTokens.Any(token => token.Kind is SyntaxKind.Marker)) ||
        (endOwner is RazorMetaCodeSyntax metaCode && metaCode.ContainsOnlyWhitespace())
    );

    private static SyntaxNode AdjustEndNode(SyntaxNode endOwner)
    {
        if (endOwner.TryGetPreviousSibling(out var previousSibling))
        {
            return previousSibling.FirstAncestorOrSelf<CSharpCodeBlockSyntax>() ?? endOwner;
        }
        return endOwner;
    }

    private static bool IsOnMarkupTag(int absoluteIndex, SyntaxNode owner)
    {
        var (startTag, endTag) = GetStartAndEndTag(owner);

        if (startTag is null)
        {
            return false;
        }

        endTag ??= startTag; // Self-closing tag

        var isOnStartTag = startTag.Span.Start <= absoluteIndex && absoluteIndex <= startTag.Span.End;
        var isOnEndTag = endTag.Span.Start <= absoluteIndex && absoluteIndex <= endTag.Span.End;

        return isOnStartTag || isOnEndTag;
    }

    private static (MarkupSyntaxNode? startTag, MarkupSyntaxNode? endTag) GetStartAndEndTag(SyntaxNode owner)
    {
        var potentialElement = owner.FirstAncestorOrSelf<MarkupSyntaxNode>(node => node is MarkupElementSyntax or MarkupTagHelperElementSyntax);

        return potentialElement switch
        {
            MarkupElementSyntax markupElement => (markupElement.StartTag, markupElement.EndTag),
            MarkupTagHelperElementSyntax tagHelper => (tagHelper.StartTag, tagHelper.EndTag),
            _ => (null, null)
        };
    }

    private static bool TryGetNamespace(RazorCodeDocument codeDocument, [NotNullWhen(returnValue: true)] out string? @namespace)
        // If the compiler can't provide a computed namespace it will fallback to "__GeneratedComponent" or
        // similar for the NamespaceNode. This would end up with extracting to a wrong namespace
        // and causing compiler errors. Avoid offering this refactoring if we can't accurately get a
        // good namespace to extract to
        => codeDocument.TryComputeNamespace(fallbackToRootNamespace: true, out @namespace);

    /// <summary>
    /// Processes a selection, modifying <paramref name="startNode"/> and <paramref name="endNode"/> in place to correct selection bounds.
    /// </summary>
    /// <param name="startNode">The starting element of the selection.</param>
    /// <param name="endNode">The ending element of the selection</param>
    /// <returns> <c>true</c> if the selection was successfully processed; otherwise, <c>false</c>.</returns>
    private static bool TryProcessSelection(
        ref SyntaxNode startNode,
        ref SyntaxNode endNode)
    {
        if (ReferenceEquals(startNode, endNode))
        {
            return true;
        }

        // If the start node contains the end node (or vice versa), we can extract the entire range
        if (startNode.Span.Contains(endNode.Span))
        {
            endNode = startNode;
            return true;
        }

        if (endNode.Span.Contains(startNode.Span))
        {
            startNode = endNode;
            return true;
        }

        // If the start element is not an ancestor of the end element (or vice versa), we need to find a common parent
        // This conditional handles cases where the user's selection spans across different levels of the DOM.
        // For example:
        //   <div>
        //     {|result:<span>
        //      {|selection:<p>Some text</p>
        //     </span>
        //     <span>
        //       <p>More text</p>
        //     </span>
        //     <span>|}|}
        //     </span>
        //   </div>
        // In this case, we need to find the smallest set of complete elements that covers the entire selection.

        // Find the closest containing sibling pair that encompasses both the start and end elements
        var (selectStart, selectEnd) = FindContainingSiblingPair(startNode, endNode);
        if (selectStart is not null && selectEnd is not null)
        {
            startNode = selectStart;
            endNode = selectEnd;

            return true;
        }

        // Note: If we don't find a valid pair, we keep the original extraction range
        return true;
        
    }

    /// <summary>
    /// Finds the smallest set of sibling nodes that contain both the start and end nodes.
    /// This is useful for determining the scope of a selection that spans across different levels of the syntax tree.
    /// </summary>
    /// <param name="startNode">The node where the selection starts.</param>
    /// <param name="endNode">The node where the selection ends.</param>
    /// <returns>A tuple containing the start and end nodes of the containing sibling pair.</returns>
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

        var startIsCodeBlock = startNode is CSharpCodeBlockSyntax;
        var endIsCodeBlock = endNode is CSharpCodeBlockSyntax;

        foreach (var child in nearestCommonAncestor.ChildNodes())
        {
            var childSpan = child.Span;

            if (startContainingNode is null &&
                childSpan.Contains(startSpan) &&
                (
                    (startIsCodeBlock && child is CSharpCodeBlockSyntax) ||
                    (!startIsCodeBlock && (child is MarkupElementSyntax or MarkupTagHelperElementSyntax))
                ))
            {
                startContainingNode = child;
            }

            if (endContainingNode is null &&
                childSpan.Contains(endSpan) &&
                (
                    (endIsCodeBlock && child is CSharpCodeBlockSyntax) ||
                    (!endIsCodeBlock && (child is MarkupElementSyntax or MarkupTagHelperElementSyntax))
                ))
            {
                endContainingNode = child;
            }

            if (startContainingNode is not null && endContainingNode is not null)
            {
                break;
            }
        }

        return (startContainingNode, endContainingNode);
    }

    private static SyntaxNode? FindNearestCommonAncestor(SyntaxNode node1, SyntaxNode node2)
    {
        for (var current = node1; current is not null; current = current.Parent)
        {
            if (IsValidAncestorNode(current) && current.Span.Contains(node2.Span))
            {
                return current;
            }
        }

        return null;
    }

    private static bool IsValidAncestorNode(SyntaxNode node) => node is MarkupElementSyntax or MarkupTagHelperElementSyntax or MarkupBlockSyntax;

    private static (bool hasOtherIdentifiers, HashSet<string> usingDirectives) GetUsingsIdentifiersInRange(SyntaxNode generalRoot, SyntaxNode scanRoot, int extractStart, int extractEnd)
    {
        var extractSpan = new TextSpan(extractStart, extractEnd - extractStart);
        var hasOtherIdentifiers = false;
        var usings = new HashSet<string>();

        // Get all using directives from the general root
        var usingsInSourceRazor = GetAllUsingDirectives(generalRoot);

        foreach (var node in scanRoot.DescendantNodes())
        {
            if (!extractSpan.Contains(node.Span))
            {
                continue;
            }

            if (!hasOtherIdentifiers)
            {
                hasOtherIdentifiers = CheckNodeForIdentifiers(node);
            }

            if (node is MarkupTagHelperElementSyntax { TagHelperInfo: { } tagHelperInfo })
            {
                AddUsingFromTagHelperInfo(tagHelperInfo, usings, usingsInSourceRazor);
            }
        }

        return (hasOtherIdentifiers, usings);
    }

    private static string[] GetAllUsingDirectives(SyntaxNode generalRoot)
    {
        using var pooledStringArray = new PooledArrayBuilder<string>();
        foreach (var node in generalRoot.DescendantNodes())
        {
            if (node.IsUsingDirective(out var children))
            {
                var sb = new StringBuilder();
                var identifierFound = false;
                var lastIdentifierIndex = -1;

                // First pass: find the last identifier
                for (var i = 0; i < children.Count; i++)
                {
                    if (children[i] is Language.Syntax.SyntaxToken token && token.Kind == Language.SyntaxKind.Identifier)
                    {
                        lastIdentifierIndex = i;
                    }
                }

                // Second pass: build the string
                for (var i = 0; i <= lastIdentifierIndex; i++)
                {
                    var child = children[i];
                    if (child is Language.Syntax.SyntaxToken tkn && tkn.Kind == Language.SyntaxKind.Identifier)
                    {
                        identifierFound = true;
                    }

                    if (identifierFound)
                    {
                        var token = child as Language.Syntax.SyntaxToken;
                        sb.Append(token?.Content);
                    }
                }

                pooledStringArray.Add(sb.ToString());
            }
        }

        return pooledStringArray.ToArray();
    }

    private static bool CheckNodeForIdentifiers(SyntaxNode node)
    {
        // This method checks for identifiers in event handlers and data bindings.

        // Even if the user doesn't reference any fields or methods from an @code block in their selection,
        // event handlers and data binding references are still expected to be passed in via parameters in the new component.
        // Hence, the call to Roslyn to get symbolic info must still be made if these are present in the extracted range.

        // An assumption I'm making that might be wrong:
        // CSharpImplicitExpressionBodySyntax, CSharpExplicitExpressionBodySyntax, and MarkupTagHelperDirectiveAttributeSyntax
        // nodes contain only one child of type CSharpExpressionLiteralSyntax

        // For MarkupTagHelperDirectiveAttributeSyntax, the syntax tree seems to show only one child of the contained CSharpExpressionLiteral as Text,
        // so it might not be worth it to check for identifiers, but only if the above is true in all cases.

        if (node is CSharpImplicitExpressionBodySyntax or CSharpExplicitExpressionBodySyntax)
        {
            var expressionLiteral = node.DescendantNodes().OfType<CSharpExpressionLiteralSyntax>().SingleOrDefault();
            if (expressionLiteral is not null)
            {
                foreach (var token in expressionLiteral.LiteralTokens)
                {
                    if (token.Kind is Language.SyntaxKind.Identifier)
                    {
                        return true;
                    }
                }
            }
        }
        else if (node is MarkupTagHelperDirectiveAttributeSyntax directiveAttribute)
        {
            var attributeDelegate = directiveAttribute.DescendantNodes().OfType<CSharpExpressionLiteralSyntax>().SingleOrDefault();
            if (attributeDelegate is not null)
            {
                if (attributeDelegate.LiteralTokens.FirstOrDefault() is Language.Syntax.SyntaxToken { Kind: Language.SyntaxKind.Text })
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AddUsingFromTagHelperInfo(TagHelperInfo tagHelperInfo, HashSet<string> usings, string[] usingsInSourceRazor)
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

                if (usingsInSourceRazor.Contains(partialNamespace))
                {
                    usings.Add(partialNamespace);
                    break;
                }
            }
        }
    }
}
