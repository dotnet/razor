// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Utilities;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

internal sealed class ExtractToComponentCodeActionResolver(
    IDocumentContextFactory documentContextFactory,
    LanguageServerFeatureOptions languageServerFeatureOptions,
    IClientConnection clientConnection,
    IDocumentVersionCache documentVersionCache,
    ITelemetryReporter telemetryReporter) : IRazorCodeActionResolver
{
    private readonly IDocumentContextFactory _documentContextFactory = documentContextFactory;
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions;
    private readonly IClientConnection _clientConnection = clientConnection;
    private readonly IDocumentVersionCache _documentVersionCache = documentVersionCache;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;

    public string Action => LanguageServerConstants.CodeActions.ExtractToComponentAction;

    public async Task<WorkspaceEdit?> ResolveAsync(JsonElement data, CancellationToken cancellationToken)
    {
        var telemetryDidSucceed = false;
        using var _ = _telemetryReporter.BeginBlock("extractToComponentResolver", Severity.Normal, new Property("didSucceed", telemetryDidSucceed));

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
        var startLinePosition = codeDocument.Source.Text.Lines.GetLinePosition(actionParams.ExtractStart);
        var endLinePosition = codeDocument.Source.Text.Lines.GetLinePosition(actionParams.ExtractEnd);

        var removeRange = VsLspFactory.CreateRange(startLinePosition, endLinePosition);

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
            actionParams,
            codeDocument,
            documentContext,
            removeRange,
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

        telemetryDidSucceed = true;
        return new WorkspaceEdit
        {
            DocumentChanges = documentChanges,
        };
    }

    /// <summary>
    /// Generates a new Razor component based on the selected content from existing markup.
    /// This method handles the extraction of code, processing of C# elements, and creation of necessary parameters.
    /// </summary>
    private async Task<NewRazorComponentInfo?> GenerateNewComponentAsync(
        ExtractToComponentCodeActionParams actionParams,
        RazorCodeDocument razorCodeDocument,
        DocumentContext documentContext,
        Range selectionRange,
        CancellationToken cancellationToken)
    {
        var contents = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (contents is null)
        {
            return null;
        }

        var sbInstance = PooledStringBuilder.GetInstance();
        var newFileContentBuilder = sbInstance.Builder;
        if (actionParams.UsingDirectives is not null)
        {
            foreach (var dependency in actionParams.UsingDirectives)
            {
                newFileContentBuilder.AppendLine($"@using {dependency}");
            }

            if (newFileContentBuilder.Length > 0)
            {
                newFileContentBuilder.AppendLine();
            }
        }

        var extractedContents = contents.GetSubTextString(
                    new TextSpan(actionParams.ExtractStart,
                    actionParams.ExtractEnd - actionParams.ExtractStart))
                .Trim();

        // Remove leading whitespace from each line to maintain proper indentation in the new component
        var extractedLines = ArrayBuilder<string>.GetInstance();
        var dedentWhitespace = actionParams.DedentWhitespaceString;
        if (!dedentWhitespace.IsNullOrEmpty())
        {
            extractedLines.AddRange(extractedContents.Split('\n'));
            for (var i = 1; i < extractedLines.Count; i++)
            {
                var line = extractedLines[i];
                if (line.StartsWith(dedentWhitespace, StringComparison.Ordinal))
                {
                    extractedLines[i] = line[dedentWhitespace.Length..];
                }
            }

            extractedContents = string.Join("\n", extractedLines);
        }

        newFileContentBuilder.Append(extractedContents);
        var result = new NewRazorComponentInfo
        {
            NewContents = newFileContentBuilder.ToString()
        };

        // Get CSharpStatements within component
        var syntaxTree = razorCodeDocument.GetSyntaxTree();
        var cSharpCodeBlocks = GetCSharpCodeBlocks(syntaxTree, actionParams.ExtractStart, actionParams.ExtractEnd, out var atCodeBlock);

        // Only make the Roslyn call if there is CSharp in the selected code
        // (code blocks, expressions, event handlers, binders) in the selected code,
        // or if the selection doesn't already include the @code block.
        // Assuming that if a user selects a @code along with markup, the @code block contains all necessary information for the component.
        if (actionParams.HasAtCodeBlock ||
            atCodeBlock is null ||
                (!actionParams.HasEventHandlerOrExpression &&
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
        
        var cSharpDocument = razorCodeDocument.GetCSharpDocument();
        var sourceMappings = cSharpDocument.SourceMappings;
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

        var componentUri = actionParams.Uri;
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
        // NOTE: This approach is not comprehensive. It capitalizes substrings that match attribute names.
        // A more correct approach would be to use the generated c# document in Roslyn and modify field symbol identifiers there,
        // then somehow build the razor content from the modified c# document, then pass that string back here.
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

        // Get all CSharpStatementLiterals except those inside @code.
        var cSharpCodeBlocks = new List<CSharpStatementLiteralSyntax>();
        var insideAtCode = false;
        CSharpStatementLiteralSyntax? tentativeAtCodeBlock = null;

        void ProcessNode(SyntaxNode node)
        {
            if (node is RazorMetaCodeSyntax razorMetaCode)
            {
                foreach (var token in razorMetaCode.MetaCode)
                {
                    if (token.Content == "code")
                    {
                        insideAtCode = true;
                        break;
                    }
                }
            }
            else if (node is CSharpStatementLiteralSyntax cSharpNode && !cSharpNode.ContainsOnlyWhitespace())
            {
                if (insideAtCode)
                {
                    tentativeAtCodeBlock = cSharpNode;
                }
                else if (span.Contains(node.Span))
                {
                    cSharpCodeBlocks.Add(cSharpNode);
                }
            }

            foreach (var child in node.ChildNodes())
            {
                ProcessNode(child);
            }

            if (insideAtCode && node is CSharpCodeBlockSyntax)
            {
                insideAtCode = false;
            }
        }

        ProcessNode(root);

        atCodeBlock = tentativeAtCodeBlock;

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
            builder.Append(method.ReturnType == "void" ? "Action" : "Func");

            // If delegate type is Action, only add generic parameters if needed. 
            if (method.ParameterTypes.Length > 0 || method.ReturnType != "void")
            {
                builder.Append('<');
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

            if ((field.IsValueType || field.Type == "string") && field.IsWrittenTo)
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

    private static void CapitalizeFieldReferences(StringBuilder content, AttributeSymbolicInfo[] attributes)
    {
        var newRazorSourceDocument = RazorSourceDocument.Create(content.ToString(), "ExtractedComponent");
        var syntaxTree = RazorSyntaxTree.Parse(newRazorSourceDocument);
        var root = syntaxTree.Root;

        // Traverse through the descendant nodes, and if we find an identifiernamesyntax whose name matches one of the attributes, capitalize it.
        foreach (var node in root.DescendantNodes())
        {
            if (node.Kind is not SyntaxKind.Identifier)
            {
                continue;
            }

            var identifierNameString = node.GetContent();
        }
    }

    private static string GenerateComponentNameAndParameters(MethodSymbolicInfo[]? methods, AttributeSymbolicInfo[]? attributes, string componentName)
    {
        if (methods is null || attributes is null)
        {
            return componentName + " ";
        }

        var builder = new StringBuilder();
        builder.Append(componentName + " ");

        foreach (var method in methods)
        {
            builder.Append($"{method.Name}=@{method.Name} ");
        }

        foreach (var attribute in attributes)
        {
            var capitalizedAttributeName = CapitalizeString(attribute.Name);
            builder.Append($"{capitalizedAttributeName}=@{attribute.Name} ");
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
