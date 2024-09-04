// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.Semantics;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Differencing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json.Linq;
using static System.Net.Mime.MediaTypeNames;
using static Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor.ExtractToComponentCodeActionProvider;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

internal sealed class ExtractToComponentCodeActionResolver(
    IDocumentContextFactory documentContextFactory,
    LanguageServerFeatureOptions languageServerFeatureOptions,
    IClientConnection clientConnection,
    IDocumentVersionCache documentVersionCache) : IRazorCodeActionResolver
{
    private readonly IDocumentContextFactory _documentContextFactory = documentContextFactory;
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions;
    private readonly IClientConnection _clientConnection = clientConnection;
    private readonly IDocumentVersionCache _documentVersionCache = documentVersionCache;

    public string Action => LanguageServerConstants.CodeActions.ExtractToComponentAction;

    public async Task<WorkspaceEdit?> ResolveAsync(JsonElement data, CancellationToken cancellationToken)
    {
        var actionParams = data.Deserialize<ExtractToComponentCodeActionParams>();
        if (actionParams is null)
        {
            return null;
        }

        if (!_documentContextFactory.TryCreate(actionParams.Uri, out var documentContext))
        {
            return null;
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var syntaxTree = codeDocument.GetSyntaxTree();

        var selectionAnalysis = TryAnalyzeSelection(codeDocument, actionParams);
        if (!selectionAnalysis.Success)
        {
            return null;
        }

        // For the purposes of determining the indentation of the extracted code, get the whitespace before the start of the selection.
        var whitespaceReferenceOwner = codeDocument.GetSyntaxTree().Root.FindInnermostNode(selectionAnalysis.ExtractStart, includeWhitespace: true).AssumeNotNull();
        var whitespaceReferenceNode = whitespaceReferenceOwner.FirstAncestorOrSelf<MarkupSyntaxNode>(node => node is MarkupElementSyntax or MarkupTagHelperElementSyntax).AssumeNotNull();
        var whitespace = string.Empty;
        if (whitespaceReferenceNode.TryGetPreviousSibling(out var startPreviousSibling) && startPreviousSibling.ContainsOnlyWhitespace())
        {
            // Get the whitespace substring so we know how much to dedent the extracted code. Remove any carriage return and newline escape characters.
            whitespace = startPreviousSibling.ToFullString();
            whitespace = whitespace.Replace("\r", string.Empty).Replace("\n", string.Empty);
        }

        var start = codeDocument.Source.Text.Lines.GetLinePosition(selectionAnalysis.ExtractStart);
        var end = codeDocument.Source.Text.Lines.GetLinePosition(selectionAnalysis.ExtractEnd);
        var removeRange = new Range
        {
            Start = new Position(start.Line, start.Character),
            End = new Position(end.Line, end.Character)
        };

        var path = FilePathNormalizer.Normalize(actionParams.Uri.GetAbsoluteOrUNCPath());
        var directoryName = Path.GetDirectoryName(path).AssumeNotNull();
        var templatePath = Path.Combine(directoryName, "Component");
        var componentPath = FileUtilities.GenerateUniquePath(templatePath, ".razor");

        // VS Code in Windows expects path to start with '/'
        var updatedComponentPath = _languageServerFeatureOptions.ReturnCodeActionAndRenamePathsWithPrefixedSlash && !componentPath.StartsWith('/')
            ? '/' + componentPath
            : componentPath;

        var newComponentUri = new UriBuilder
        {
            Scheme = Uri.UriSchemeFile,
            Path = updatedComponentPath,
            Host = string.Empty,
        }.Uri;

        var componentName = Path.GetFileNameWithoutExtension(componentPath);
        var newComponentResult = await GenerateNewComponentAsync(
            selectionAnalysis,
            codeDocument,
            actionParams.Uri,
            documentContext,
            removeRange,
            whitespace,
            cancellationToken).ConfigureAwait(false);

        if (newComponentResult is null)
        {
            return null;
        }

        var newComponentContent = newComponentResult.NewContents;
        var componentNameAndParams = GenerateComponentNameAndParameters(newComponentResult.Methods, newComponentResult.Attributes, componentName);

        var componentDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier { Uri = actionParams.Uri };
        var newComponentDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier { Uri = newComponentUri };

        var documentChanges = new SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[]
        {
            new CreateFile { Uri = newComponentUri },
            new TextDocumentEdit
            {
                TextDocument = componentDocumentIdentifier,
                Edits =
                [
                    new TextEdit
                    {
                        NewText = $"<{componentNameAndParams}/>",
                        Range = removeRange,
                    }
                ],
            },
            new TextDocumentEdit
            {
                TextDocument = newComponentDocumentIdentifier,
                Edits  =
                [
                    new TextEdit
                    {
                        NewText = newComponentContent,
                        Range = new Range { Start = new Position(0, 0), End = new Position(0, 0) },
                    }
                ],
            }
        };

        return new WorkspaceEdit
        {
            DocumentChanges = documentChanges,
        };
    }

    internal sealed record SelectionAnalysisResult
    {
        public required bool Success;
        public int ExtractStart;
        public int ExtractEnd;
        public bool HasAtCodeBlock;
        public bool HasEventHandlerOrExpression;
        public HashSet<string>? UsingDirectives;
    }

    private static SelectionAnalysisResult TryAnalyzeSelection(RazorCodeDocument codeDocument, ExtractToComponentCodeActionParams actionParams)
    {
        var syntaxTree = codeDocument.GetSyntaxTree();
        var sourceText = codeDocument.Source.Text;

        var (startElementNode, endElementNode) = GetStartAndEndElements(sourceText, syntaxTree, actionParams);
        if (startElementNode is null)
        {
            return new SelectionAnalysisResult { Success = false };
        }

        endElementNode ??= startElementNode;
        
        var success = TryProcessSelection(startElementNode,
            endElementNode,
            codeDocument,
            actionParams,
            out var extractStart,
            out var extractEnd,
            out var hasAtCodeBlock);

        if (!success)
        {
            return new SelectionAnalysisResult { Success = false };
        }

        var dependencyScanRoot = FindNearestCommonAncestor(startElementNode, endElementNode) ?? startElementNode;
        var usingDirectives = GetUsingDirectivesInRange(syntaxTree, dependencyScanRoot, extractStart, extractEnd);
        var hasOtherIdentifiers = CheckHasOtherIdentifiers(dependencyScanRoot, extractStart, extractEnd);

        return new SelectionAnalysisResult
        {
            Success = success,
            ExtractStart = extractStart,
            ExtractEnd = extractEnd,
            HasAtCodeBlock = hasAtCodeBlock,
            HasEventHandlerOrExpression = hasOtherIdentifiers,
            UsingDirectives = usingDirectives,
        };
    }

    private static (MarkupSyntaxNode? Start, MarkupSyntaxNode? End) GetStartAndEndElements(SourceText sourceText, RazorSyntaxTree syntaxTree, ExtractToComponentCodeActionParams actionParams)
    {
        var owner = syntaxTree.Root.FindInnermostNode(actionParams.AbsoluteIndex, includeWhitespace: true);
        if (owner is null)
        {
            return (null, null);
        }

        var startElementNode = owner.FirstAncestorOrSelf<MarkupSyntaxNode>(node => node is MarkupTagHelperElementSyntax or MarkupElementSyntax);
        if (startElementNode is null)
        {
            return (null, null);
        }

        var endElementNode = GetEndElementNode(sourceText, syntaxTree, actionParams);

        return (startElementNode, endElementNode);
    }

    private static MarkupSyntaxNode? GetEndElementNode(SourceText sourceText, RazorSyntaxTree syntaxTree, ExtractToComponentCodeActionParams actionParams)
    {
        var selectionStart = actionParams.SelectStart;
        var selectionEnd = actionParams.SelectEnd;

        if (selectionStart == selectionEnd)
        {
            return null;
        }

        var endAbsoluteIndex = sourceText.GetRequiredAbsoluteIndex(selectionEnd);
        var endOwner = syntaxTree.Root.FindInnermostNode(endAbsoluteIndex, true);
        if (endOwner is null)
        {
            return null;
        }

        // Correct selection to include the current node if the selection ends at the "edge" (i.e. immediately after the ">") of a tag.
        if (endOwner is MarkupTextLiteralSyntax && endOwner.ContainsOnlyWhitespace() && endOwner.TryGetPreviousSibling(out var previousSibling))
        {
            endOwner = previousSibling;
        }

        return endOwner.FirstAncestorOrSelf<MarkupSyntaxNode>(node => node is MarkupTagHelperElementSyntax or MarkupElementSyntax);
    }

    /// <summary>
    /// Processes a selection, providing the start and end of the extraction range if successful.
    /// </summary>
    /// <param name="startElementNode">The starting element of the selection.</param>
    /// <param name="endElementNode">The ending element of the selection</param>
    /// <param name="codeDocument">The code document containing the selection.</param>
    /// <param name="actionParams">The parameters for the extraction action</param>
    /// <param name="extractStart">The start of the extraction range.</param>
    /// <param name="extractEnd">The end of the extraction range</param>
    /// <param name="hasCodeBlock">Whether the selection has a @code block</param>
    /// <returns> <c>true</c> if the selection was successfully processed; otherwise, <c>false</c>.</returns>
    private static bool TryProcessSelection(
        MarkupSyntaxNode startElementNode,
        MarkupSyntaxNode endElementNode,
        RazorCodeDocument codeDocument,
        ExtractToComponentCodeActionParams actionParams,
        out int extractStart,
        out int extractEnd,
        out bool hasCodeBlock)
    {
        extractStart = startElementNode.Span.Start;
        extractEnd = endElementNode.Span.End;
        hasCodeBlock = false;

        // Check if it's a multi-point selection
        if (actionParams.SelectStart == actionParams.SelectEnd)
        {
            return true;
        }

        // If the start node contains the end node (or vice versa), we can extract the entire range
        if (endElementNode.Ancestors().Contains(startElementNode))
        {
            extractEnd = startElementNode.Span.End;
            return true;
        }

        if (startElementNode.Ancestors().Contains(endElementNode))
        {
            extractStart = endElementNode.Span.Start;
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
        if (startElementNode != endElementNode)
        {
            // Find the closest containing sibling pair that encompasses both the start and end elements
            var (selectStart, selectEnd) = FindContainingSiblingPair(startElementNode, endElementNode);
            if (selectStart is not null && selectEnd is not null)
            {
                extractStart = selectStart.Span.Start;
                extractEnd = selectEnd.Span.End;
                return true;
            }

            // Note: If we don't find a valid pair, we keep the original extraction range
            return true;
        }

        var endLocation = codeDocument.Source.Text.GetRequiredAbsoluteIndex(actionParams.SelectEnd);

        var endOwner = codeDocument.GetSyntaxTree().Root.FindInnermostNode(endLocation, true);
        var endCodeBlock = endOwner?.FirstAncestorOrSelf<CSharpCodeBlockSyntax>();
        if (endOwner is not null && endOwner.TryGetPreviousSibling(out var previousSibling))
        {
            endCodeBlock = previousSibling.FirstAncestorOrSelf<CSharpCodeBlockSyntax>();
        }

        if (endCodeBlock is not null)
        {
            hasCodeBlock = true;
            var (withCodeBlockStart, withCodeBlockEnd) = FindContainingSiblingPair(startElementNode, endCodeBlock);
            extractStart = withCodeBlockStart?.Span.Start ?? extractStart;
            extractEnd = withCodeBlockEnd?.Span.End ?? extractEnd;
        }

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

        var endIsCodeBlock = endNode is CSharpCodeBlockSyntax;

        foreach (var child in nearestCommonAncestor.ChildNodes())
        {
            if (!IsValidNode(child, endIsCodeBlock))
            {
                continue;
            }

            var childSpan = child.Span;
            if (startContainingNode is null && childSpan.Contains(startSpan))
            {
                startContainingNode = child;
            }

            // Check if this child contains the end node
            if (childSpan.Contains(endSpan))
            {
                endContainingNode = child;
            }

            // If we've found both containing nodes, we can stop searching
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

    private static bool IsValidNode(SyntaxNode node, bool isCodeBlock)
    {
        return node is MarkupElementSyntax or MarkupTagHelperElementSyntax || (isCodeBlock && node is CSharpCodeBlockSyntax);
    }

    private static HashSet<string> GetUsingDirectivesInRange(RazorSyntaxTree syntaxTree, SyntaxNode root, int extractStart, int extractEnd)
    {
        // The new component usings are going to be a subset of the usings in the source razor file.
        using var pooledStringArray = new PooledArrayBuilder<string>();
        foreach (var node in syntaxTree.Root.DescendantNodes())
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

        var usingsInSourceRazor = pooledStringArray.ToArray();

        var usings = new HashSet<string>();
        var extractSpan = new TextSpan(extractStart, extractEnd - extractStart);

        // Only analyze nodes within the extract span
        foreach (var node in root.DescendantNodes())
        {
            if (!extractSpan.Contains(node.Span))
            {
                continue;
            }

            if (node is MarkupTagHelperElementSyntax { TagHelperInfo: { } tagHelperInfo })
            {
                AddUsingFromTagHelperInfo(tagHelperInfo, usings, usingsInSourceRazor);
            }
        }

        return usings;
    }

    private static bool CheckHasOtherIdentifiers(SyntaxNode root, int extractStart, int extractEnd)
    {
        var extractSpan = new TextSpan(extractStart, extractEnd - extractStart);

        foreach (var node in root.DescendantNodes())
        {
            if (!extractSpan.Contains(node.Span))
            {
                continue;
            }

            // An assumption I'm making that might be wrong:
            // CSharpImplicitExpressionBodySyntax, CSharpExplicitExpressionBodySyntax, and MarkupTagHelperDirectiveAttributeSyntax
            // nodes contain only one child of type CSharpExpressionLiteralSyntax

            // For MarkupTagHelperDirectiveAttributeSyntax, the syntax tree seems to show only one child of the contained CSharpExpressionLiteral as Text,
            // so it might not be worth it to check for identifiers, but only if the above is true in all cases.

            if (node is CSharpImplicitExpressionBodySyntax or CSharpExplicitExpressionBodySyntax)
            {

                var expressionLiteral = node.DescendantNodes().OfType<CSharpExpressionLiteralSyntax>().SingleOrDefault();
                if (expressionLiteral is null)
                {
                    continue;
                }

                foreach (var token in expressionLiteral.LiteralTokens)
                {
                    if (token.Kind is Language.SyntaxKind.Identifier)
                    {
                        return true;
                    }
                }
            }
            else if (node is MarkupTagHelperDirectiveAttributeSyntax directiveAttribute) 
            {
                var attributeDelegate = directiveAttribute.DescendantNodes().OfType<CSharpExpressionLiteralSyntax>().SingleOrDefault();
                if (attributeDelegate is null)
                {
                    continue;
                }

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

    /// <summary>
    /// Generates a new Razor component based on the selected content from existing markup.
    /// This method handles the extraction of code, processing of C# elements, and creation of necessary parameters.
    /// </summary>
    private async Task<NewRazorComponentInfo?> GenerateNewComponentAsync(
        SelectionAnalysisResult selectionAnalysis,
        RazorCodeDocument razorCodeDocument,
        Uri componentUri,
        DocumentContext documentContext,
        Range selectionRange,
        string whitespace,
        CancellationToken cancellationToken)
    {
        var contents = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (contents is null)
        {
            return null;
        }

        var sbInstance = PooledStringBuilder.GetInstance();
        var newFileContentBuilder = sbInstance.Builder;
        if (selectionAnalysis.UsingDirectives is not null)
        {
            foreach (var dependency in selectionAnalysis.UsingDirectives)
            {
                newFileContentBuilder.AppendLine($"@using {dependency}");
            }

            if (newFileContentBuilder.Length > 0)
            {
                newFileContentBuilder.AppendLine();
            }
        }

        var extractedContents = contents.GetSubTextString(
                    new TextSpan(selectionAnalysis.ExtractStart,
                    selectionAnalysis.ExtractEnd - selectionAnalysis.ExtractStart))
                .Trim();

        // Remove leading whitespace from each line to maintain proper indentation in the new component
        var extractedLines = extractedContents.Split('\n');
        for (var i = 1; i < extractedLines.Length; i++)
        {
            var line = extractedLines[i];
            if (line.StartsWith(whitespace, StringComparison.Ordinal))
            {
                extractedLines[i] = line[whitespace.Length..];
            }
        }

        extractedContents = string.Join("\n", extractedLines);
        newFileContentBuilder.Append(extractedContents);

        var result = new NewRazorComponentInfo
        {
            NewContents = newFileContentBuilder.ToString()
        };

        // Get CSharpStatements within component
        var syntaxTree = razorCodeDocument.GetSyntaxTree();
        var cSharpCodeBlocks = GetCSharpCodeBlocks(syntaxTree, selectionAnalysis.ExtractStart, selectionAnalysis.ExtractEnd, out var atCodeBlock);

        // Only make the Roslyn call if there is CSharp in the selected code
        // (code blocks, expressions, event handlers, binders) in the selected code,
        // or if the selection doesn't already include the @code block.
        // Assuming that if a user selects a @code along with markup, the @code block contains all necessary information for the component.
        if (selectionAnalysis.HasAtCodeBlock ||
            atCodeBlock is null ||
                (!selectionAnalysis.HasEventHandlerOrExpression &&
                cSharpCodeBlocks.Count == 0))
        {
            sbInstance.Free();
            return result;
        }

        if (!_documentVersionCache.TryGetDocumentVersion(documentContext.Snapshot, out var version))
        {
            sbInstance.Free();
            throw new InvalidOperationException("Failed to retrieve document version.");
        }
        
        var sourceMappings = razorCodeDocument.GetCSharpDocument().SourceMappings;
        var cSharpDocument = razorCodeDocument.GetCSharpDocument();
        var sourceText = razorCodeDocument.Source.Text;
        var generatedSourceText = SourceText.From(cSharpDocument.GeneratedCode);

        // Create mappings between the original Razor source and the generated C# code
        var sourceMappingRanges = sourceMappings.Select(m =>
        (
            OriginalRange: RazorDiagnosticConverter.ConvertSpanToRange(m.OriginalSpan, sourceText),
            m.GeneratedSpan
        )).ToArray();

        // Find the spans in the generated C# code that correspond to the selected Razor code
        var intersectingGeneratedSpans = sourceMappingRanges
            .Where(m => m.OriginalRange != null && selectionRange.IntersectsOrTouches(m.OriginalRange))
            .Select(m => m.GeneratedSpan)
            .ToArray();

        var intersectingGeneratedRanges = intersectingGeneratedSpans
            .Select(m =>RazorDiagnosticConverter.ConvertSpanToRange(m, generatedSourceText))
            .Where(range => range != null)
            .Select(range => range!)
            .ToArray();

        var parameters = new GetSymbolicInfoParams()
        {
            Project = new TextDocumentIdentifier
            {
                Uri = new Uri(documentContext.Project.FilePath, UriKind.Absolute)
            },
            Document = new TextDocumentIdentifier
            {
                Uri = componentUri
            },
            HostDocumentVersion = version.Value,
            GeneratedDocumentRanges = intersectingGeneratedRanges
        };

        MemberSymbolicInfo? componentInfo;

        // Send a request to the language server to get symbolic information about the extracted code
        try
        {
            componentInfo = await _clientConnection.SendRequestAsync<GetSymbolicInfoParams, MemberSymbolicInfo?>(
                CustomMessageNames.RazorGetSymbolicInfoEndpointName,
                parameters,
                cancellationToken: default).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to send request to Roslyn endpoint", ex);
        }

        if (componentInfo is null)
        {
            sbInstance.Free();
            throw new InvalidOperationException("Roslyn endpoint 'GetSymbolicInfo' returned null");
        }

        // Generate parameter declarations for methods and attributes used in the extracted component
        var promotedMethods = GeneratePromotedMethods(componentInfo.Methods);
        var promotedAttributes = GeneratePromotedAttributes(componentInfo.Attributes, Path.GetFileName(componentUri.LocalPath));
        var newFileCodeBlock = GenerateNewFileCodeBlock(promotedMethods, promotedAttributes);

        // Capitalize attribute references in the new component to match C# naming conventions
        foreach (var attribute in componentInfo.Attributes)
        {
            var capitalizedAttributeName = CapitalizeString(attribute.Name);
            newFileContentBuilder.Replace(attribute.Name, capitalizedAttributeName);
        }

        newFileContentBuilder.Append(newFileCodeBlock);

        result.NewContents = newFileContentBuilder.ToString();
        result.Methods = componentInfo.Methods;
        result.Attributes = componentInfo.Attributes;

        sbInstance.Free();
        return result;
    }

    private static List<CSharpStatementLiteralSyntax> GetCSharpCodeBlocks(
        RazorSyntaxTree syntaxTree,
        int start,
        int end,
        out CSharpStatementLiteralSyntax? atCodeBlock)
    {
        var root = syntaxTree.Root;
        var span = new TextSpan(start, end - start);

        // Get only CSharpSyntaxNodes without Razor Meta Code as ancestors. This avoids getting the @code block at the end of a razor file.
        var cSharpCodeBlocks = root.DescendantNodes()
        .Where(node => span.Contains(node.Span))
        .OfType<CSharpStatementLiteralSyntax>()
        .Where(cSharpNode =>
            !cSharpNode.Ancestors().OfType<RazorMetaCodeSyntax>().Any())
        .ToList();

        atCodeBlock = root.DescendantNodes().OfType<CSharpStatementLiteralSyntax>().LastOrDefault();
        atCodeBlock = atCodeBlock is not null && cSharpCodeBlocks.Contains(atCodeBlock) ? null : atCodeBlock;

        return cSharpCodeBlocks;
    }

    // Create a series of [Parameter] attributes for extracted methods.
    // Void return functions are promoted to Action<T> delegates.
    // All other functions should be Func<T... TResult> delegates.
    private static string GeneratePromotedMethods(MethodSymbolicInfo[] methods)
    {
        var builder = new StringBuilder();
        var parameterCount = 0;
        var totalMethods = methods.Length;

        foreach (var method in methods)
        {
            builder.AppendLine($"\t/// Delegate for the '{method.Name}' method.");
            builder.AppendLine("\t[Parameter]");

            // Start building delegate type
            builder.Append("\trequired public ");
            builder.Append(method.ReturnType == "Void" ? "Action" : "Func");

            // If delegate type is Action, only add generic parameters if needed. 
            if (method.ParameterTypes.Length > 0 || method.ReturnType != "Void")
            {
                builder.Append('<');
                builder.Append(string.Join(", ", method.ParameterTypes));

                if (method.ReturnType != "Void")
                {
                    if (method.ParameterTypes.Length > 0)
                    {
                        // Add one last comma in the list of generic parameters for the result: "<..., TResult>"
                        builder.Append(", ");
                    }

                    builder.Append(method.ReturnType);
                }

                builder.Append('>');
            }

            builder.Append($" {method.Name} {{ get; set; }}");
            if (parameterCount++ < totalMethods - 1)
            {
                // Space between methods except for the last method.
                builder.AppendLine();
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private static string GeneratePromotedAttributes(AttributeSymbolicInfo[] attributes, string? sourceDocumentFileName)
    {
        var builder = new StringBuilder();
        var attributeCount = 0;
        var totalAttributes = attributes.Length;

        foreach (var field in attributes)
        {
            var capitalizedFieldName = CapitalizeString(field.Name);

            if ((field.IsValueType || field.Type == "String") && field.IsWrittenTo)
            {
                builder.AppendLine($"\t// Warning: Field '{capitalizedFieldName}' was passed by value and may not be referenced correctly. Please check its usage in the original document: '{sourceDocumentFileName}'.");
            }

            builder.AppendLine("\t[Parameter]");

            // Members cannot be less visible than their enclosing type, so we don't need to check for private fields.
            builder.Append($"\trequired public {field.Type} {capitalizedFieldName} {{ get; set; }}");

            if (attributeCount++ < totalAttributes - 1)
            {
                builder.AppendLine();
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    // Most likely out of scope for the class, could be moved elsewhere
    private static string CapitalizeString(string str)
    {
        return str.Length > 0
            ? char.ToUpper(str[0]) + str[1..]
            : str;
    }

    private static string GenerateNewFileCodeBlock(string promotedMethods, string promotedProperties)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("@code {");
        if (promotedProperties.Length > 0)
        {
            builder.AppendLine(promotedProperties);
        }

        if (promotedProperties.Length > 0 && promotedMethods.Length > 0)
        {
            builder.AppendLine();
        }

        if (promotedMethods.Length > 0)
        {
            builder.AppendLine(promotedMethods);
        }
        builder.Append("}");
        return builder.ToString();
    }

    private static string GenerateComponentNameAndParameters(MethodSymbolicInfo[]? methods, AttributeSymbolicInfo[]? attributes, string componentName)
    {
        if (methods is null || attributes is null)
        {
            return componentName;
        }

        var builder = new StringBuilder();
        builder.Append(componentName + " ");

        foreach (var method in methods)
        {
            builder.Append($"{method.Name}={method.Name} ");
        }

        foreach (var attribute in attributes)
        {
            var capitalizedAttributeName = CapitalizeString(attribute.Name);
            builder.Append($"{capitalizedAttributeName}={attribute.Name} ");
        }

        return builder.ToString();
    }

    internal sealed record NewRazorComponentInfo
    {
        public required string NewContents { get; set; }
        public MethodSymbolicInfo[]? Methods { get; set; }
        public AttributeSymbolicInfo[]? Attributes { get; set; }
    }
}
