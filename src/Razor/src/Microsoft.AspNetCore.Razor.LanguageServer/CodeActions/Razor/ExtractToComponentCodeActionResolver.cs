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
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using System.Reflection.Metadata.Ecma335;
using Microsoft.VisualStudio.Utilities;
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

    public string Action => LanguageServerConstants.CodeActions.ExtractToComponentAction;

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
        var newComponentResult = await GenerateNewComponentAsync(selectionAnalysis, codeDocument, actionParams.Uri, documentContext, removeRange, cancellationToken).ConfigureAwait(false);

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

    private static ExtractToComponentCodeActionParams? DeserializeActionParams(JsonElement data)
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
        public HashSet<string>? UsingDirectives;
        public HashSet<string>? TentativeVariableDependencies;
    }

    private static SelectionAnalysisResult TryAnalyzeSelection(RazorCodeDocument codeDocument, ExtractToComponentCodeActionParams actionParams)
    {
        var (startElementNode, endElementNode) = GetStartAndEndElements(codeDocument, actionParams);
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
            out var extractEnd);

        if (!success)
        {
            return new SelectionAnalysisResult { Success = false };
        }

        var dependencyScanRoot = FindNearestCommonAncestor(startElementNode, endElementNode) ?? startElementNode;
        var componentDependencies = AddUsingDirectivesInRange(dependencyScanRoot, extractStart, extractEnd);
        var variableDependencies = AddVariableDependenciesInRange(dependencyScanRoot, extractStart, extractEnd);

        return new SelectionAnalysisResult
        {
            Success = success,
            ExtractStart = extractStart,
            ExtractEnd = extractEnd,
            UsingDirectives = componentDependencies,
            TentativeVariableDependencies = variableDependencies,
        };
    }

    private static (MarkupSyntaxNode? Start, MarkupSyntaxNode? End) GetStartAndEndElements(RazorCodeDocument codeDocument, ExtractToComponentCodeActionParams actionParams)
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

        var startElementNode = owner.FirstAncestorOrSelf<MarkupSyntaxNode>(node => node is MarkupTagHelperElementSyntax or MarkupElementSyntax);
        if (startElementNode is null)
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

    private static MarkupSyntaxNode? TryGetEndElementNode(Position selectionStart, Position selectionEnd, RazorSyntaxTree syntaxTree, SourceText sourceText)
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
        if (endOwner is MarkupTextLiteralSyntax && endOwner.ContainsOnlyWhitespace() && endOwner.TryGetPreviousSibling(out var previousSibling))
        {
            endOwner = previousSibling;
        }

        return endOwner.FirstAncestorOrSelf<MarkupSyntaxNode>(node => node is MarkupTagHelperElementSyntax or MarkupElementSyntax);
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
    /// Processes a selection, providing the start and end of the extraction range if successful.
    /// </summary>
    /// <param name="startElementNode">The starting element of the selection.</param>
    /// <param name="endElementNode">The ending element of the selection</param>
    /// <param name="codeDocument">The code document containing the selection.</param>
    /// <param name="actionParams">The parameters for the extraction action</param>
    /// <param name="extractStart">The start of the extraction range.</param>
    /// <param name="extractEnd">The end of the extraction range</param>
    /// <returns> <c>true</c> if the selection was successfully processed; otherwise, <c>false</c>.</returns>
    private static bool TryProcessSelection(MarkupSyntaxNode startElementNode, MarkupSyntaxNode endElementNode, RazorCodeDocument codeDocument, ExtractToComponentCodeActionParams actionParams, out int extractStart, out int extractEnd)
    {
        extractStart = startElementNode.Span.Start;
        extractEnd = endElementNode.Span.End;

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

        var endLocation = GetEndLocation(actionParams.SelectEnd, codeDocument.GetSourceText());
        if (!endLocation.HasValue)
        {
            return false;
        }

        var endOwner = codeDocument.GetSyntaxTree().Root.FindInnermostNode(endLocation.Value.AbsoluteIndex, true);
        var endCodeBlock = endOwner?.FirstAncestorOrSelf<CSharpCodeBlockSyntax>();
        if (endOwner is not null && endOwner.TryGetPreviousSibling(out var previousSibling))
        {
            endCodeBlock = previousSibling.FirstAncestorOrSelf<CSharpCodeBlockSyntax>();
        }

        if (endCodeBlock is not null)
        {
            var (withCodeBlockStart, withCodeBlockEnd) = FindContainingSiblingPair(startElementNode, endCodeBlock);
            extractStart = withCodeBlockStart?.Span.Start ?? extractStart;
            extractEnd = withCodeBlockEnd?.Span.End ?? extractEnd;
        }

        return true;
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

            if (childSpan.Contains(endSpan))
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

    private static bool IsValidNode(SyntaxNode node, bool isCodeBlock)
    {
        return node is MarkupElementSyntax or MarkupTagHelperElementSyntax || (isCodeBlock && node is CSharpCodeBlockSyntax);
    }

    private static HashSet<string> AddUsingDirectivesInRange(SyntaxNode root, int extractStart, int extractEnd)
    {
        var usings = new HashSet<string>();
        var extractSpan = new TextSpan(extractStart, extractEnd - extractStart);

        // Only analyze nodes within the extract span
        foreach (var node in root.DescendantNodes().Where(node => extractSpan.Contains(node.Span)))
        {
            if (node is MarkupTagHelperElementSyntax { TagHelperInfo: { } tagHelperInfo })
            {
                AddUsingFromTagHelperInfo(tagHelperInfo, usings);
            }
        }

        return usings;
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

    private static void AddUsingFromTagHelperInfo(TagHelperInfo tagHelperInfo, HashSet<string> dependencies)
    {
        foreach (var descriptor in tagHelperInfo.BindingResult.Descriptors)
        {
            if (descriptor is null)
            {
                continue;
            }

            var typeNamespace = descriptor.GetTypeNamespace();
            dependencies.Add(typeNamespace);
        }
    }

    private async Task<NewRazorComponentInfo?> GenerateNewComponentAsync(
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

        var inst = PooledStringBuilder.GetInstance();
        var newFileContentBuilder = inst.Builder;
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

        newFileContentBuilder.Append(extractedContents);

        // Get CSharpStatements within component
        var syntaxTree = razorCodeDocument.GetSyntaxTree();
        var cSharpCodeBlocks = GetCSharpCodeBlocks(syntaxTree, selectionAnalysis.ExtractStart, selectionAnalysis.ExtractEnd);

        var result = new NewRazorComponentInfo
        {
            NewContents = newFileContentBuilder.ToString(),
            Methods = []
        };

        // Only make the Roslyn call if there is valid CSharp in the selected code.
        if (cSharpCodeBlocks.Count == 0)
        {
            inst.Free();
            return result;
        }

        if (!_documentVersionCache.TryGetDocumentVersion(documentContext.Snapshot, out var version))
        {
            inst.Free();
            return result;
        }

        var sourceMappings = razorCodeDocument.GetCSharpDocument().SourceMappings;
        var sourceMappingRanges = sourceMappings.Select(m =>
        (
            new Range
            {
                Start = new Position(m.OriginalSpan.LineIndex, m.OriginalSpan.CharacterIndex),
                End = new Position(m.OriginalSpan.LineIndex, m.OriginalSpan.EndCharacterIndex)
            },
            m.GeneratedSpan
         )).ToList();

        var relevantTextSpan = relevantRange.ToTextSpan(razorCodeDocument.Source.Text);
        var intersectingGeneratedSpans = sourceMappingRanges.Where(m => relevantRange.IntersectsOrTouches(m.Item1)).Select(m => m.GeneratedSpan).ToArray();

        // I'm not sure why, but for some reason the endCharacterIndex is lower than the CharacterIndex so they must be swapped.
        var intersectingGeneratedRanges = intersectingGeneratedSpans.Select(m =>
        (
            new Range
            {
                Start = new Position(m.LineIndex, m.EndCharacterIndex),
                End = new Position(m.LineIndex, m.CharacterIndex)
            }
        )).ToArray();

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
            IntersectingRangesInGeneratedMappings = intersectingGeneratedRanges
        };

        SymbolicInfo? componentInfo;
        try
        {
            componentInfo = await _clientConnection.SendRequestAsync<GetSymbolicInfoParams, SymbolicInfo?>(
                CustomMessageNames.RazorGetSymbolicInfoEndpointName,
                parameters,
                cancellationToken: default).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to send request to RazorComponentInfoEndpoint", ex);
        }

        if (componentInfo is null)
        {
            inst.Free();
            return result;
        }

        var codeBlockAtEnd = GetCodeBlockAtEnd(syntaxTree);
        if (codeBlockAtEnd is null)
        {
            inst.Free();
            return result;
        }

        var identifiersInCodeBlock = GetIdentifiersInContext(codeBlockAtEnd, cSharpCodeBlocks);
        if (componentInfo.Methods is null)
        {
            inst.Free();
            return result;
        }

        var methodsInFile = componentInfo.Methods.Select(method => method.Name).ToHashSet();
        var methodStringsInContext = methodsInFile.Intersect(identifiersInCodeBlock);
        var methodsInContext = GetMethodsInContext(componentInfo, methodStringsInContext);
        var promotedMethods = GeneratePromotedMethods(methodsInContext);

        var fieldsInContext = GetFieldsInContext(componentInfo.Fields, identifiersInCodeBlock);
        var forwardedFields = GenerateForwardedConstantFields(fieldsInContext, Path.GetFileName(razorCodeDocument.Source.FilePath));

        var newFileCodeBlock = GenerateNewFileCodeBlock(promotedMethods, forwardedFields);

        ReplaceMethodInvocations(newFileContentBuilder, methodsInContext);
        newFileContentBuilder.Append(newFileCodeBlock);

        result.NewContents = newFileContentBuilder.ToString();
        result.Methods = methodsInContext;

        inst.Free();
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

    private static HashSet<MethodSymbolicInfo> GetMethodsInContext(SymbolicInfo componentInfo, IEnumerable<string> methodStringsInContext)
    {
        var methodsInContext = new HashSet<MethodSymbolicInfo>();
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
    private static string GeneratePromotedMethods(HashSet<MethodSymbolicInfo> methods)
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

            // Start building delegate type
            builder.Append("public ");
            builder.Append(method.ReturnType == "void" ? "Action" : "Func");

            // If delegate type is Action, only add generic parameters if needed. 
            if (method.ParameterTypes.Length > 0 || method.ReturnType != "void")
            {
                builder.Append("<");
                builder.Append(string.Join(", ", method.ParameterTypes));

                if (method.ReturnType != "void")
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

            builder.Append($"Parameter{(parameterCount > 0 ? parameterCount : "")} {{ get; set; }}");
            if (parameterCount < totalMethods - 1)
            {
                // Space between methods except for the last method.
                builder.AppendLine();
                builder.AppendLine();
            }

            parameterCount++;
        }

        return builder.ToString();
    }

    private static HashSet<FieldSymbolicInfo> GetFieldsInContext(FieldSymbolicInfo[] fields, HashSet<string> identifiersInCodeBlock)
    {
        if (fields is null)
        {
            return [];
        }

        var fieldsInContext = new HashSet<FieldSymbolicInfo>();

        foreach (var fieldInfo in fields)
        {
            if (identifiersInCodeBlock.Contains(fieldInfo.Name))
            {
                fieldsInContext.Add(fieldInfo);
            }
        }

        return fieldsInContext;
    }

    private static string GenerateForwardedConstantFields(HashSet<FieldSymbolicInfo> relevantFields, string? sourceDocumentFileName)
    {
        var builder = new StringBuilder();
        var fieldCount = 0;
        var totalFields = relevantFields.Count;

        foreach (var field in relevantFields)
        {
            if (field.IsValueType || field.Type == "string")
            {
                builder.AppendLine($"// Warning: Field '{field.Name}' was passed by value and may not be referenced correctly. Please check its usage in the original document: '{sourceDocumentFileName}'.");
            }

            builder.AppendLine($"public {field.Type} {field.Name}");

            if (fieldCount < totalFields - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
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

    // Method invocations in the new file must be replaced with their respective parameter name.
    private static void ReplaceMethodInvocations(StringBuilder newFileContentBuilder, HashSet<MethodSymbolicInfo> methods)
    {
        var parameterCount = 0;
        foreach (var method in methods)
        {
            newFileContentBuilder.Replace(method.Name, $"Parameter{(parameterCount > 0 ? parameterCount : "")}");
            parameterCount++;
        }
    }

    private static string GenerateComponentNameAndParameters(HashSet<MethodSymbolicInfo>? methods, string componentName)
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
            builder.Append(' ');
            parameterCount++;
        }

        return builder.ToString();
    }

    internal sealed record NewRazorComponentInfo
    {
        public required string NewContents { get; set; }
        public required HashSet<MethodSymbolicInfo>? Methods { get; set; }
    }
}
