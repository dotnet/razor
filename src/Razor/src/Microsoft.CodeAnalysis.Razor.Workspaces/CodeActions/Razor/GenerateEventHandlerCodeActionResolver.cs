// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.CodeActions.Razor;

internal class GenerateEventHandlerCodeActionResolver(
    IRoslynCodeActionHelpers roslynCodeActionHelpers,
    IRazorFormattingService razorFormattingService,
    IFileSystem fileSystem) : IRazorCodeActionResolver
{
    private readonly IRoslynCodeActionHelpers _roslynCodeActionHelpers = roslynCodeActionHelpers;
    private readonly IRazorFormattingService _razorFormattingService = razorFormattingService;
    private readonly IFileSystem _fileSystem = fileSystem;

    private const string BeginningIndents = $"{FormattingUtilities.InitialIndent}{FormattingUtilities.Indent}";

    public string Action => LanguageServerConstants.CodeActions.GenerateEventHandler;

    public async Task<WorkspaceEdit?> ResolveAsync(DocumentContext documentContext, JsonElement data, RazorFormattingOptions options, CancellationToken cancellationToken)
    {
        var actionParams = data.Deserialize<GenerateEventHandlerCodeActionParams>();
        if (actionParams is null)
        {
            return null;
        }

        var code = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var uriPath = FilePathNormalizer.Normalize(documentContext.Uri.GetAbsoluteOrUNCPath());
        var razorClassName = Path.GetFileNameWithoutExtension(uriPath);
        var codeBehindPath = $"{uriPath}.cs";

        // If there is no code behind file with a name we expect, then generate a code block
        if (!_fileSystem.FileExists(codeBehindPath))
        {
            return await GenerateEventHandlerInCodeBlockAsync(
                code,
                actionParams,
                documentContext,
                options,
                cancellationToken).ConfigureAwait(false);
        }

        // TODO: Update IFileSystem.ReadFile(...) to return a SourceText without reading a huge string.
        var content = _fileSystem.ReadFile(codeBehindPath);
        var text = SourceText.From(content, Encoding.UTF8);
        if (razorClassName is null ||
            !code.TryGetNamespace(fallbackToRootNamespace: true, out var razorNamespace) ||
            GetCSharpClassDeclarationSyntax(text, razorNamespace, razorClassName) is not { } @class)
        {
            // The code behind file is malformed, generate the code in the razor file instead.
            return await GenerateEventHandlerInCodeBlockAsync(
                code,
                actionParams,
                documentContext,
                options,
                cancellationToken).ConfigureAwait(false);
        }

        var codeBehindUri = LspFactory.CreateFilePathUri(codeBehindPath);

        var codeBehindTextDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier() { DocumentUri = new(codeBehindUri) };

        var templateWithMethodSignature = PopulateEventHandlerSignature(actionParams, useIndentPlaceholders: true);
        var classLocationLineSpan = @class.GetLocation().GetLineSpan();
        var formattedMethod = FormattingUtilities.AddIndentationToMethod(
            templateWithMethodSignature,
            options.TabSize,
            options.InsertSpaces,
            @class.SpanStart,
            classLocationLineSpan.StartLinePosition.Character,
            text);

        var edit = LspFactory.CreateTextEdit(
            line: classLocationLineSpan.EndLinePosition.Line,
            character: 0,
            $"{formattedMethod}{Environment.NewLine}");

        var result = await _roslynCodeActionHelpers.GetSimplifiedTextEditsAsync(documentContext, codeBehindUri, edit, cancellationToken).ConfigureAwait(false);

        var codeBehindTextDocEdit = new TextDocumentEdit()
        {
            TextDocument = codeBehindTextDocumentIdentifier,
            Edits = [.. result ?? [edit]]
        };

        return new WorkspaceEdit() { DocumentChanges = new[] { codeBehindTextDocEdit } };
    }

    private async Task<WorkspaceEdit?> GenerateEventHandlerInCodeBlockAsync(
        RazorCodeDocument code,
        GenerateEventHandlerCodeActionParams actionParams,
        DocumentContext documentContext,
        RazorFormattingOptions options,
        CancellationToken cancellationToken)
    {
        var csharpSyntaxTree = await documentContext.Snapshot.GetCSharpSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        var csharpSyntaxRoot = await csharpSyntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
        if (!csharpSyntaxRoot.TryGetClassDeclaration(out var classDecl))
        {
            return null;
        }

        // We are going to arbitrarily place the method at the end of the class in the generated C# file. The formatting service will fix indentation, and put it in an appropriate
        // place in the Razor file
        var templateWithMethodSignature = PopulateEventHandlerSignature(actionParams, useIndentPlaceholders: false);
        var classLocationLineSpan = classDecl.CloseBraceToken.GetLocation().GetLineSpan();
        var tempTextEdit = LspFactory.CreateTextEdit(
            line: classLocationLineSpan.StartLinePosition.Line,
            character: 0,
            templateWithMethodSignature + Environment.NewLine);

        // Call the simplifier to reduce things like `global::System.Threading.Task` down to just `Task`, if possible.
        var result = await _roslynCodeActionHelpers.GetSimplifiedTextEditsAsync(documentContext, codeBehindUri: null, tempTextEdit, cancellationToken).ConfigureAwait(false);
        if (result is not { } edits)
        {
            return null;
        }

        // Run the changes through the formatter to handle the rest. This is the same as what the CSharpCodeActionResolver does, so out generated method ends up going through the
        // same pipeline as the Roslyn Generate Method code action.
        var csharpSourceText = code.GetCSharpSourceText();
        var csharpTextChanges = edits.SelectAsArray(csharpSourceText.GetTextChange);
        var formattedChange = await _razorFormattingService.TryGetCSharpCodeActionEditAsync(documentContext, csharpTextChanges, options, cancellationToken).ConfigureAwait(false);
        if (formattedChange is not { } razorChange)
        {
            return null;
        }

        return new WorkspaceEdit()
        {
            DocumentChanges = new[] {
                new TextDocumentEdit()
                {
                    TextDocument = new OptionalVersionedTextDocumentIdentifier() { DocumentUri = new(documentContext.Uri) },
                    Edits = [code.Source.Text.GetTextEdit(razorChange)],
                }
            }
        };
    }

    private static string PopulateEventHandlerSignature(GenerateEventHandlerCodeActionParams actionParams, bool useIndentPlaceholders)
    {
        var returnType = actionParams.IsAsync
            ? "global::System.Threading.Tasks.Task"
            : "void";

        var parameters = actionParams.EventParameterType is null
            ? string.Empty // Couldn't find the params, generate no params instead.
            : $"global::{actionParams.EventParameterType} args";

        var beginningIndent = useIndentPlaceholders ? BeginningIndents : "";
        var indent = useIndentPlaceholders ? FormattingUtilities.Indent : "    ";

        return $$"""
            {{beginningIndent}}private {{returnType}} {{actionParams.MethodName}}({{parameters}})
            {{beginningIndent}}{
            {{beginningIndent}}{{indent}}throw new global::System.NotImplementedException();
            {{beginningIndent}}}
            """;
    }

    private static ClassDeclarationSyntax? GetCSharpClassDeclarationSyntax(SourceText csharpContent, string razorNamespace, string razorClassName)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(csharpContent);
        return GetCSharpClassDeclarationSyntax(syntaxTree, razorNamespace, razorClassName);
    }

    private static ClassDeclarationSyntax? GetCSharpClassDeclarationSyntax(SyntaxTree csharpSyntaxTree, string razorNamespace, string razorClassName)
    {
        var compilationUnit = csharpSyntaxTree.GetCompilationUnitRoot();
        var @namespace = compilationUnit.Members
            .FirstOrDefault(m => m is BaseNamespaceDeclarationSyntax { } @namespace && @namespace.Name.ToString() == razorNamespace);
        if (@namespace is null)
        {
            return null;
        }

        var @class = ((BaseNamespaceDeclarationSyntax)@namespace).Members
            .FirstOrDefault(m => m is ClassDeclarationSyntax { } @class && razorClassName == @class.Identifier.Text);
        if (@class is null)
        {
            return null;
        }

        return (ClassDeclarationSyntax)@class;
    }
}
