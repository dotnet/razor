// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.CodeAnalysis.Differencing;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using ICSharpCode.Decompiler.Semantics;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using System.Security.AccessControl;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;
using static Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor.ExtractToComponentCodeActionProvider;
using Microsoft.VisualStudio.Text;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

internal sealed class ExtractToComponentCodeActionResolver(
    IDocumentContextFactory documentContextFactory,
    RazorLSPOptionsMonitor razorLSPOptionsMonitor,
    LanguageServerFeatureOptions languageServerFeatureOptions,
    IClientConnection clientConnection,
    IRazorFormattingService razorFormattingService,
    IDocumentVersionCache documentVersionCache) : IRazorCodeActionResolver
{
    private static readonly Workspace s_workspace = new AdhocWorkspace();

    private readonly IDocumentContextFactory _documentContextFactory = documentContextFactory;
    private readonly RazorLSPOptionsMonitor _razorLSPOptionsMonitor = razorLSPOptionsMonitor;
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions;
    private readonly IClientConnection _clientConnection = clientConnection;
    private readonly IRazorFormattingService _razorFormattingService = razorFormattingService;
    private readonly IDocumentVersionCache _documentVersionCache = documentVersionCache;

    public string Action => LanguageServerConstants.CodeActions.ExtractToNewComponentAction;

    public async Task<WorkspaceEdit?> ResolveAsync(JsonElement data, CancellationToken cancellationToken)
    {
        if (data.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        var actionParams = JsonSerializer.Deserialize<ExtractToComponentCodeActionParams>(data.GetRawText());
        if (actionParams is null)
        {
            return null;
        }

        if (!_documentContextFactory.TryCreate(actionParams.Uri, out var documentContext))
        {
            return null;
        }

        var componentDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        if (componentDocument.IsUnsupported())
        {
            return null;
        }

        var selectionAnalysis = TryAnalyzeSelection(componentDocument, actionParams);

        if (!selectionAnalysis.Success)
        {
            return null;
        }

        var start = componentDocument.Source.Text.Lines.GetLinePosition(selectionAnalysis.ExtractStart);
        var end = componentDocument.Source.Text.Lines.GetLinePosition(selectionAnalysis.ExtractEnd);
        var removeRange = new Range
        {
            Start = new Position(start.Line, start.Character),
            End = new Position(end.Line, end.Character)
        };

        if (!FileKinds.IsComponent(componentDocument.GetFileKind()))
        {
            return null;
        }

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
        var newComponentContent = await GenerateNewComponentAsync(selectionAnalysis, componentDocument, actionParams.Uri, documentContext, removeRange, cancellationToken).ConfigureAwait(false);

        if (newComponentContent is null)
        {
            return null;
        }

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
                        NewText = $"<{componentName} />",
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

        //if (!_documentContextFactory.TryCreateForOpenDocument(newComponentUri, out var versionedDocumentContext))
        //{
        //    throw new InvalidOperationException("Failed to create a versioned document context for the new component");
        //}

        //var formattingOptions = new VisualStudio.LanguageServer.Protocol.FormattingOptions()
        //{
        //    TabSize = _razorLSPOptionsMonitor.CurrentValue.TabSize,
        //    InsertSpaces = _razorLSPOptionsMonitor.CurrentValue.InsertSpaces,
        //    OtherOptions = new Dictionary<string, object>
        //    {
        //        { "trimTrailingWhitespace", true },
        //        { "insertFinalNewline", true },
        //        { "trimFinalNewlines", true },
        //    },
        //};
        
        //TextEdit[]? formattedEdits;
        //try
        //{
        //    formattedEdits = await _razorFormattingService.FormatAsync(
        //        documentContext,
        //        range: removeRange,
        //        formattingOptions,
        //        cancellationToken: default).ConfigureAwait(false);
        //}
        //catch (Exception ex)
        //{
        //    throw new InvalidOperationException("Failed to format the new component", ex);
        //}

        return new WorkspaceEdit
        {
            DocumentChanges = documentChanges,
        };
    }

    internal sealed record SelectionAnalysisResult
    {
        public required bool Success;
        public required int ExtractStart;
        public required int ExtractEnd;
        public required HashSet<string> ComponentDependencies;
    }

    private static SelectionAnalysisResult TryAnalyzeSelection(RazorCodeDocument codeDocument, ExtractToComponentCodeActionParams actionParams)
    {
        var result = new SelectionAnalysisResult
        {
            Success = false,
            ExtractStart = 0,
            ExtractEnd = 0,
            ComponentDependencies = [],
        };

        var (startElementNode, endElementNode) = GetStartAndEndElements(codeDocument, actionParams);
        if (startElementNode is null)
        {
            return result;
        }

        endElementNode ??= startElementNode;

        var (success, extractStart, extractEnd) = TryProcessMultiPointSelection(startElementNode, endElementNode, codeDocument, actionParams);

        var dependencyScanRoot = FindNearestCommonAncestor(startElementNode, endElementNode) ?? startElementNode;
        var dependencies = AddComponentDependenciesInRange(dependencyScanRoot, extractStart, extractEnd);

        result.Success = success;
        result.ExtractStart = extractStart;
        result.ExtractEnd = extractEnd;
        result.ComponentDependencies = dependencies;

        return result;
    }

    private static (MarkupElementSyntax? Start, MarkupElementSyntax? End) GetStartAndEndElements(RazorCodeDocument codeDocument, ExtractToComponentCodeActionParams actionParams)
    {
        var syntaxTree = codeDocument.GetSyntaxTree();
        if (syntaxTree is null)
        {
            return (null, null);
        }

        var owner = syntaxTree.Root.FindInnermostNode(actionParams.AbsoluteIndex, includeWhitespace: true);
        if (owner is null)
        {
            return (null, null);
        }

        var startElementNode = owner.FirstAncestorOrSelf<MarkupElementSyntax>();
        if (startElementNode is null || IsInsideProperHtmlContent(actionParams.AbsoluteIndex, startElementNode))
        {
            return (null, null);
        }

        var sourceText = codeDocument.GetSourceText();
        if (sourceText is null)
        {
            return (null, null);
        }

        var endElementNode = TryGetEndElementNode(actionParams.SelectStart, actionParams.SelectEnd, syntaxTree, sourceText);

        return (startElementNode, endElementNode);
    }

    private static bool IsInsideProperHtmlContent(int absoluteIndex, MarkupElementSyntax startElementNode)
    {
        // If the provider executes before the user/completion inserts an end tag, the below return fails
        if (startElementNode.EndTag.IsMissing)
        {
            return true;
        }

        return absoluteIndex > startElementNode.StartTag.Span.End &&
               absoluteIndex < startElementNode.EndTag.SpanStart;
    }

    private static MarkupElementSyntax? TryGetEndElementNode(Position selectionStart, Position selectionEnd, RazorSyntaxTree syntaxTree, SourceText sourceText)
    {
        if (selectionStart == selectionEnd)
        {
            return null;
        }

        var endLocation = GetEndLocation(selectionEnd, sourceText);
        if (!endLocation.HasValue)
        {
            return null;
        }

        var endOwner = syntaxTree.Root.FindInnermostNode(endLocation.Value.AbsoluteIndex, true);

        if (endOwner is null)
        {
            return null;
        }

        // Correct selection to include the current node if the selection ends at the "edge" (i.e. immediately after the ">") of a tag.
        if (string.IsNullOrWhiteSpace(endOwner.ToFullString()) && endOwner.TryGetPreviousSibling(out var previousSibling))
        {
            endOwner = previousSibling;
        }

        return endOwner?.FirstAncestorOrSelf<MarkupElementSyntax>();
    }

    private static SourceLocation? GetEndLocation(Position selectionEnd, SourceText sourceText)
    {
        if (!selectionEnd.TryGetSourceLocation(sourceText, logger: default,  out var location))
        {
            return null;
        }

        return location;
    }

    /// <summary>
    /// Processes a multi-point selection to determine the correct range for extraction.
    /// </summary>
    /// <param name="startElementNode">The starting element of the selection.</param>
    /// <param name="endElementNode">The ending element of the selection, if it exists.</param>
    /// <param name="codeDocument">The code document containing the selection.</param>
    /// <param name="actionParams">The parameters for the extraction action, which will be updated.</param>
    /// one more line for output
    /// <returns>A tuple containing a boolean indicating success, the start of the extraction range, and the end of the extraction range.</returns>
    private static (bool success, int extractStart, int extractEnd) TryProcessMultiPointSelection(MarkupElementSyntax startElementNode, MarkupElementSyntax endElementNode, RazorCodeDocument codeDocument, ExtractToComponentCodeActionParams actionParams)
    {
        var extractStart = startElementNode.Span.Start;
        var extractEnd = endElementNode.Span.End;

        // Check if it's a multi-point selection
        if (actionParams.SelectStart == actionParams.SelectEnd)
        {
            return (true, extractStart, extractEnd);
        }

        // Check if the start element is an ancestor of the end element or vice versa
        var selectionStartHasParentElement = endElementNode.Ancestors().Any(node => node == startElementNode);
        var selectionEndHasParentElement = startElementNode.Ancestors().Any(node => node == endElementNode);

        // If the start element is an ancestor of the end element (or vice versa), update the extraction range
        extractStart = selectionEndHasParentElement ? endElementNode.Span.Start : extractStart;
        extractEnd = selectionStartHasParentElement ? startElementNode.Span.End : extractEnd;

        // If the start element is not an ancestor of the end element (or vice versa), we need to find a common parent
        // This conditional handles cases where the user's selection spans across different levels of the DOM.
        // For example:
        //   <div>
        //     <span>
        //      Selected text starts here<p>Some text</p>
        //     </span>
        //     <span>
        //       <p>More text</p>
        //     </span>
        //     Selected text ends here <span></span>
        //   </div>
        // In this case, we need to find the smallest set of complete elements that covers the entire selection.
        if (startElementNode != endElementNode && !(selectionStartHasParentElement || selectionEndHasParentElement))
        {
            // Find the closest containing sibling pair that encompasses both the start and end elements
            var (selectStart, selectEnd) = FindContainingSiblingPair(startElementNode, endElementNode);

            // If we found a valid containing pair, update the extraction range
            if (selectStart is not null && selectEnd is not null)
            {
                extractStart = selectStart.Span.Start;
                extractEnd = selectEnd.Span.End;

                return (true, extractStart, extractEnd);
            }
            // Note: If we don't find a valid pair, we keep the original extraction range
        }

        if (startElementNode != endElementNode)
        {
            return (true, extractStart, extractEnd); // Will only trigger when the end of the selection does not include a code block.
        }

        var endLocation = GetEndLocation(actionParams.SelectEnd, codeDocument.GetSourceText());
        if (!endLocation.HasValue)
        {
            return (false, extractStart, extractEnd);
        }

        var endOwner = codeDocument.GetSyntaxTree().Root.FindInnermostNode(endLocation.Value.AbsoluteIndex, true);
        var endCodeBlock = endOwner?.FirstAncestorOrSelf<CSharpCodeBlockSyntax>();
        if (endOwner is not null && endOwner.TryGetPreviousSibling(out var previousSibling))
        {
            endCodeBlock = previousSibling.FirstAncestorOrSelf<CSharpCodeBlockSyntax>();
        }

        if (endCodeBlock is null)
        {
            // One of the cases where this triggers is when a single element is multi-pointedly selected
            return (true, extractStart, extractEnd);
        }

        var (withCodeBlockStart, withCodeBlockEnd) = FindContainingSiblingPair(startElementNode, endCodeBlock);

        // If selection ends on code block, set the extract end to the end of the code block.
        extractStart = withCodeBlockStart?.Span.Start ?? extractStart;
        extractEnd = withCodeBlockEnd?.Span.End ?? extractEnd;

        return (true, extractStart, extractEnd);
    }

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

        foreach (var child in nearestCommonAncestor.ChildNodes().Where(node => IsValidNode(node, endIsCodeBlock)))
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
        var secondNodeIsCodeBlock = node2 is CSharpCodeBlockSyntax;

        while (current is not null)
        {
            if (ShouldCheckNode(current, secondNodeIsCodeBlock) && current.Span.Contains(node2.Span))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }

    // Whenever the multi point selection includes a code block at the end, the logic for finding the nearest common ancestor and containing sibling pair
    // should accept nodes of type MarkupBlockSyntax and CSharpCodeBlock each, respectively. ShouldCheckNode() and IsValidNode() handle these cases.
    private static bool ShouldCheckNode(SyntaxNode node, bool isCodeBlock)
    {
        if (isCodeBlock)
        {
            return node is MarkupElementSyntax or MarkupBlockSyntax;
        }

        return node is MarkupElementSyntax;
    }

    private static bool IsValidNode(SyntaxNode node, bool isCodeBlock)
    {
        return node is MarkupElementSyntax || (isCodeBlock && node is CSharpCodeBlockSyntax);
    }

    private static HashSet<string> AddComponentDependenciesInRange(SyntaxNode root, int extractStart, int extractEnd)
    {
        var dependencies = new HashSet<string>();
        var extractSpan = new TextSpan(extractStart, extractEnd - extractStart);

        // Only analyze nodes within the extract span
        foreach (var node in root.DescendantNodes().Where(node => extractSpan.Contains(node.Span)))
        {
            if (node is MarkupTagHelperElementSyntax)
            {
                var tagHelperInfo = GetTagHelperInfo(node);
                if (tagHelperInfo is not null)
                {
                    AddDependenciesFromTagHelperInfo(tagHelperInfo, ref dependencies);
                }
            }
        }

        return dependencies;
    }

    private static TagHelperInfo? GetTagHelperInfo(SyntaxNode node)
    {
        if (node is MarkupTagHelperElementSyntax markupElement)
        {
            return markupElement.TagHelperInfo;
        }

        return null;
    }

    private static void AddDependenciesFromTagHelperInfo(TagHelperInfo tagHelperInfo, ref HashSet<string> dependencies)
    {
        foreach (var descriptor in tagHelperInfo.BindingResult.Descriptors)
        {
            if (descriptor is not null)
            {
                foreach (var metadata in descriptor.Metadata)
                {
                    if (metadata.Key == TagHelperMetadata.Common.TypeNamespace &&
                        metadata.Value is not null &&
                        !dependencies.Contains($"@using {metadata.Value}"))
                    {
                        dependencies.Add($"@using {metadata.Value}");
                    }
                }
            }
        }
    }

    private async Task<string?> GenerateNewComponentAsync(
        SelectionAnalysisResult selectionAnalysis,
        RazorCodeDocument razorCodeDocument,
        Uri componentUri,
        DocumentContext documentContext,
        Range relevantRange,
        CancellationToken cancellationToken)
    {
        var contents = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (contents is null)
        {
            return null;
        }

        var dependencies = string.Join(Environment.NewLine, selectionAnalysis.ComponentDependencies);
        var extractedContents = contents.GetSubTextString(new CodeAnalysis.Text.TextSpan(selectionAnalysis.ExtractStart, selectionAnalysis.ExtractEnd - selectionAnalysis.ExtractStart)).Trim();
        var newFileContent = $"{dependencies}{(dependencies.Length > 0 ? Environment.NewLine + Environment.NewLine : "")}{extractedContents}";

        // Get CSharpStatements within component
        var syntaxTree = razorCodeDocument.GetSyntaxTree();
        var cSharpCodeBlocks = GetCSharpCodeBlocks(syntaxTree, selectionAnalysis.ExtractStart, selectionAnalysis.ExtractEnd);

        // Only make the Roslyn call if there is valid CSharp in the selected code.
        if (cSharpCodeBlocks.Count == 0)
        {
            return newFileContent;
        }

        if(!_documentVersionCache.TryGetDocumentVersion(documentContext.Snapshot, out var version))
        {
            return newFileContent;
        }

        var parameters = new RazorComponentInfoParams()
        {
            Project = new TextDocumentIdentifier
            {
                Uri = new Uri(documentContext.Project.FilePath, UriKind.Absolute)
            },
            Document = new TextDocumentIdentifier
            {
                Uri = componentUri
            },
            ScanRange = relevantRange,
            HostDocumentVersion = version.Value
        };

        RazorComponentInfo? componentInfo;

        try
        {
            componentInfo = await _clientConnection.SendRequestAsync<RazorComponentInfoParams, RazorComponentInfo?>(CustomMessageNames.RazorComponentInfoEndpointName, parameters, cancellationToken: default).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to send request to RazorComponentInfoEndpoint", ex);
        }

        // Check if client connection call was successful
        if (componentInfo is null)
        {
            return newFileContent;
        }

        return newFileContent;
    }

    private static List<CSharpCodeBlockSyntax> GetCSharpCodeBlocks(RazorSyntaxTree syntaxTree, int start, int end)
    {
        var root = syntaxTree.Root;
        var span = new TextSpan(start, end - start);

        // Get only CSharpSyntaxNodes without Razor Directives as children or ancestors. This avoids getting the @code block at the end of a razor file.
        var razorDirectives = root.DescendantNodes()
            .Where(node => node.SpanStart >= start && node.Span.End <= end)
            .OfType<RazorDirectiveSyntax>();

        var cSharpCodeBlocks = root.DescendantNodes()
        .Where(node => span.Contains(node.Span))
        .OfType<CSharpCodeBlockSyntax>()
        .Where(csharpNode =>
            !csharpNode.Ancestors().OfType<RazorDirectiveSyntax>().Any() &&
            !razorDirectives.Any(directive => directive.Span.Contains(csharpNode.Span)))
        .ToList();

        return cSharpCodeBlocks;
    }

    // Get identifiers in code block to union with the identifiers in the extracted code
    private static List<string> GetIdentifiers(RazorSyntaxTree syntaxTree)
    {
        var identifiers = new List<string>();
        var root = syntaxTree.Root;
        // Get only the last CSharpCodeBlock (has an explicit "@code" transition)
        var cSharpCodeBlock = root.DescendantNodes().OfType<CSharpCodeBlockSyntax>().LastOrDefault();

        if (cSharpCodeBlock == null)
        {
            return identifiers;
        }

        foreach (var node in cSharpCodeBlock.DescendantNodes())
        {
            if (node is CSharpStatementLiteralSyntax literal && literal.Kind is Language.SyntaxKind.Identifier)
            {
                var lit = literal.ToFullString();
            }
        }

        return identifiers;

        //var cSharpSyntaxNodes = cSharpCodeBlock.DescendantNodes().OfType<>();
    }
}
