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
using ICSharpCode.Decompiler.CSharp.Syntax;
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
        var actionParams = DeserializeActionParams(data);
        if (actionParams is null)
        {
            return null;
        }

        if (!_documentContextFactory.TryCreate(actionParams.Uri, out var documentContext))
        {
            return null;
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        if (codeDocument.IsUnsupported())
        {
            return null;
        }

        if (!FileKinds.IsComponent(codeDocument.GetFileKind()))
        {
            return null;
        }

        var selectionAnalysis = TryAnalyzeSelection(codeDocument, actionParams);
        if (!selectionAnalysis.Success)
        {
            return null;
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
        var newComponentResult = await GenerateNewComponentAsync(selectionAnalysis, codeDocument, actionParams.Uri, newComponentUri, documentContext, cancellationToken).ConfigureAwait(false);

        if (newComponentResult is null)
        {
            return null;
        }

        var newComponentContent = newComponentResult.NewContents;
        var componentNameAndParams = GenerateComponentNameAndParameters(newComponentResult.Methods, componentName);
            
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
    private ExtractToComponentCodeActionParams? DeserializeActionParams(JsonElement data)
    {
        return data.ValueKind == JsonValueKind.Undefined
            ? null
            : JsonSerializer.Deserialize<ExtractToComponentCodeActionParams>(data.GetRawText());
    }

    internal sealed record SelectionAnalysisResult
    {
        public required bool Success;
        public int ExtractStart;
        public int ExtractEnd;
        public HashSet<string>? ComponentDependencies;
        public HashSet<string>? VariableDependencies;
    }

    private static SelectionAnalysisResult TryAnalyzeSelection(RazorCodeDocument codeDocument, ExtractToComponentCodeActionParams actionParams)
    {
        var (startElementNode, endElementNode) = GetStartAndEndElements(codeDocument, actionParams);
        if (startElementNode is null)
        {
            return new SelectionAnalysisResult { Success = false };
        }

        endElementNode ??= startElementNode;

        var (success, extractStart, extractEnd) = TryProcessMultiPointSelection(startElementNode, endElementNode, codeDocument, actionParams);

        var dependencyScanRoot = FindNearestCommonAncestor(startElementNode, endElementNode) ?? startElementNode;
        var methodDependencies = AddComponentDependenciesInRange(dependencyScanRoot, extractStart, extractEnd);
        var variableDependencies = AddVariableDependenciesInRange(dependencyScanRoot, extractStart, extractEnd);

        return new SelectionAnalysisResult
        {
            Success = success,
            ExtractStart = extractStart,
            ExtractEnd = extractEnd,
            ComponentDependencies = methodDependencies,
            VariableDependencies = variableDependencies,
        };
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

        return endOwner.FirstAncestorOrSelf<MarkupElementSyntax>();
    }

    private static SourceLocation? GetEndLocation(Position selectionEnd, SourceText sourceText)
    {
        if (!selectionEnd.TryGetSourceLocation(sourceText, logger: default, out var location))
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

    private static HashSet<string> AddVariableDependenciesInRange(SyntaxNode root, int extractStart, int extractEnd)
    {
        var dependencies = new HashSet<string>();
        var extractSpan = new TextSpan(extractStart, extractEnd - extractStart);

        var candidates = root.DescendantNodes().Where(node => extractSpan.Contains(node.Span));

        foreach (var node in root.DescendantNodes().Where(node => extractSpan.Contains(node.Span)))
        {
            if (node is MarkupTagHelperAttributeValueSyntax tagAttribute)
            {
                dependencies.Add(tagAttribute.ToFullString());
            }
            else if (node is CSharpImplicitExpressionBodySyntax implicitExpression)
            {
                dependencies.Add(implicitExpression.ToFullString());
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

    private async Task<NewRazorComponentInfo?> GenerateNewComponentAsync(
        SelectionAnalysisResult selectionAnalysis,
        RazorCodeDocument razorCodeDocument,
        Uri componentUri,
        Uri newComponentUri,
        DocumentContext documentContext,
        CancellationToken cancellationToken)
    {
        var contents = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (contents is null)
        {
            return null;
        }

        var dependencies = selectionAnalysis.ComponentDependencies is not null
        ? string.Join(Environment.NewLine, selectionAnalysis.ComponentDependencies)
        : string.Empty;

        var extractedContents = contents.GetSubTextString(new CodeAnalysis.Text.TextSpan(selectionAnalysis.ExtractStart, selectionAnalysis.ExtractEnd - selectionAnalysis.ExtractStart)).Trim();
        var newFileContent = $"{dependencies}{(dependencies.Length > 0 ? Environment.NewLine + Environment.NewLine : "")}{extractedContents}";

        // Get CSharpStatements within component
        var syntaxTree = razorCodeDocument.GetSyntaxTree();
        var cSharpCodeBlocks = GetCSharpCodeBlocks(syntaxTree, selectionAnalysis.ExtractStart, selectionAnalysis.ExtractEnd);

        var result = new NewRazorComponentInfo
        {
            NewContents = newFileContent,
            Methods = []
        };

        // Only make the Roslyn call if there is valid CSharp in the selected code.
        if (cSharpCodeBlocks.Count == 0)
        {
            return result;
        }

        if (!_documentVersionCache.TryGetDocumentVersion(documentContext.Snapshot, out var version))
        {
            return result;
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
            NewDocument = new TextDocumentIdentifier
            {
                Uri = newComponentUri
            },
            NewContents = newFileContent,
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
            return result;
        }

        var codeBlockAtEnd = GetCodeBlockAtEnd(syntaxTree);
        if (codeBlockAtEnd is null)
        {
            return result;
        }

        var identifiersInCodeBlock = GetIdentifiersInContext(codeBlockAtEnd, cSharpCodeBlocks);

        if (componentInfo.Methods is null)
        {
            return result;
        }

        var methodsInFile = componentInfo.Methods.Select(method => method.Name).ToHashSet();
        var methodStringsInContext = methodsInFile.Intersect(identifiersInCodeBlock);
        var methodsInContext = GetMethodsInContext(componentInfo, methodStringsInContext);
        var promotedMethods = GeneratePromotedMethods(methodsInContext);

        var fieldsInContext = GetFieldsInContext(componentInfo, identifiersInCodeBlock);
        var forwardedFields = GenerateForwardedConstantFields(codeBlockAtEnd, fieldsInContext);
        var newFileCodeBlock = GenerateNewFileCodeBlock(promotedMethods, forwardedFields);

        newFileContent = ReplaceMethodInvocations(newFileContent, methodsInContext);
        newFileContent += newFileCodeBlock;

        result.NewContents = newFileContent;
        result.Methods = methodsInContext;

        return result;
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
    private static HashSet<string> GetIdentifiersInContext(SyntaxNode codeBlockAtEnd, List<CSharpCodeBlockSyntax> previousCodeBlocks)
    {
        var identifiersInLastCodeBlock = new HashSet<string>();
        var identifiersInPreviousCodeBlocks = new HashSet<string>();

        if (codeBlockAtEnd == null)
        {
            return identifiersInLastCodeBlock;
        }

        foreach (var node in codeBlockAtEnd.DescendantNodes())
        {
            if (node.Kind is Language.SyntaxKind.Identifier)
            {
                var lit = node.ToFullString();
                identifiersInLastCodeBlock.Add(lit);
            }
        }

        foreach (var previousCodeBlock in previousCodeBlocks)
        {
            foreach (var node in previousCodeBlock.DescendantNodes())
            {
                if (node.Kind is Language.SyntaxKind.Identifier)
                {
                    var lit = node.ToFullString();
                    identifiersInPreviousCodeBlocks.Add(lit);
                }
            }
        }

        // Now union with identifiers in other cSharpCodeBlocks in context
        identifiersInLastCodeBlock.IntersectWith(identifiersInPreviousCodeBlocks);

        return identifiersInLastCodeBlock;
    }

    private static HashSet<MethodInsideRazorElementInfo> GetMethodsInContext(RazorComponentInfo componentInfo, IEnumerable<string> methodStringsInContext)
    {
        var methodsInContext = new HashSet<MethodInsideRazorElementInfo>();
        if (componentInfo.Methods is null)
        {
            return methodsInContext;
        }

        foreach (var componentMethod in componentInfo.Methods)
        {
            if (methodStringsInContext.Contains(componentMethod.Name) && !methodsInContext.Any(method => method.Name == componentMethod.Name))
            {
                methodsInContext.Add(componentMethod);
            }
        }

        return methodsInContext;
    }

    private static SyntaxNode? GetCodeBlockAtEnd(RazorSyntaxTree syntaxTree)
    {
        var root = syntaxTree.Root;

        // Get only the last CSharpCodeBlock (has an explicit "@code" transition)
        var razorDirectiveAtEnd = root.DescendantNodes().OfType<RazorDirectiveSyntax>().LastOrDefault();

        if (razorDirectiveAtEnd is null)
        {
            return null;
        }

        return razorDirectiveAtEnd.Parent;
    }

    // Create a series of [Parameter] attributes for extracted methods.
    // Void return functions are promoted to Action<T> delegates.
    // All other functions should be Func<T... TResult> delegates.
    private static string GeneratePromotedMethods(HashSet<MethodInsideRazorElementInfo> methods)
    {
        var builder = new StringBuilder();
        var parameterCount = 0;
        var totalMethods = methods.Count;

        foreach (var method in methods)
        {
            builder.AppendLine("/// <summary>");
            builder.AppendLine($"/// Delegate for the '{method.Name}' method.");
            builder.AppendLine("/// </summary>");
            builder.AppendLine("[Parameter]");
            builder.Append("public ");

            if (method.ReturnType == "void")
            {
                builder.Append("Action");
            }
            else
            {
                builder.Append("Func<");
            }

            if (method.ParameterTypes.Count > 0)
            {
                if (method.ReturnType == "void")
                {
                    builder.Append("<");
                }

                builder.Append(string.Join(", ", method.ParameterTypes));
                if (method.ReturnType != "void")
                {
                    builder.Append(", ");
                }
            }

            if (method.ReturnType != "void")
            {
                builder.Append(method.ReturnType);
            }

            builder.Append($"{(method.ReturnType == "void" ? (method.ParameterTypes.Count > 0 ? ">" : "") : ">")}? " +
                           $"Parameter{(parameterCount > 0 ? parameterCount : "")} {{ get; set; }}");
            if (parameterCount < totalMethods - 1)
            {
                builder.AppendLine();
                builder.AppendLine();
            }

            parameterCount++;
        }

        return builder.ToString();
    }

    private static string GenerateForwardedConstantFields(SyntaxNode codeBlockAtEnd, HashSet<string> relevantFields)
    {
        var builder = new StringBuilder();

        var codeBlockString = codeBlockAtEnd.ToFullString();

        var lines = codeBlockString.Split('\n');
        foreach (var line in lines)
        {
            if (relevantFields.Any(field => line.Contains(field)))
            {
                builder.AppendLine(line.Trim());
            }
        }

        return builder.ToString();
    }

    // GetFieldsInContext(componentInfo, identifiersInCodeBlock)
    private static HashSet<string> GetFieldsInContext(RazorComponentInfo componentInfo, HashSet<string> identifiersInCodeBlock)
    {
        if (componentInfo.Fields is null)
        {
            return [];
        }

        var identifiersInFile = componentInfo.Fields.Select(field => field.Name).ToHashSet();
        return identifiersInFile.Intersect(identifiersInCodeBlock).ToHashSet();
    }

    private static string GenerateNewFileCodeBlock(string promotedMethods, string carryoverFields)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("@code {");
        builder.AppendLine(carryoverFields);
        builder.AppendLine(promotedMethods);
        builder.AppendLine("}");
        return builder.ToString();
    }

    // Method invocations in the new file must be replaced with their respective parameter name. This is simply a case of replacing each string.
    private static string ReplaceMethodInvocations(string newFileContent, HashSet<MethodInsideRazorElementInfo> methods)
    {
        var parameterCount = 0;
        foreach (var method in methods)
        {
            newFileContent = newFileContent.Replace(method.Name, $"Parameter{(parameterCount > 0 ? parameterCount : "")}");
            parameterCount++;
        }

        return newFileContent;
    }

    private static string GenerateComponentNameAndParameters(HashSet<MethodInsideRazorElementInfo>? methods, string componentName)
    {
        var builder = new StringBuilder();
        builder.Append(componentName + " ");
        var parameterCount = 0;

        if (methods is null)
        {
            return builder.ToString();
        }

        foreach (var method in methods)
        {
            builder.Append($"Parameter{(parameterCount > 0 ? parameterCount : "")}");
            builder.Append($"={method.Name}");
            builder.Append(" ");
            parameterCount++;
        }

        return builder.ToString();
    }

    internal sealed record NewRazorComponentInfo
    {
        public required string NewContents { get; set; }
        public required HashSet<MethodInsideRazorElementInfo>? Methods { get; set; }
    }
}
