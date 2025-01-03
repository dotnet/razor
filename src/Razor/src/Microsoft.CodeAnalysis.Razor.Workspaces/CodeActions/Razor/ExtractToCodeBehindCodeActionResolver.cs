﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.CodeActions.Models;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.CodeActions;

internal class ExtractToCodeBehindCodeActionResolver(
    LanguageServerFeatureOptions languageServerFeatureOptions,
    IRoslynCodeActionHelpers roslynCodeActionHelpers) : IRazorCodeActionResolver
{
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions;
    private readonly IRoslynCodeActionHelpers _roslynCodeActionHelpers = roslynCodeActionHelpers;

    public string Action => LanguageServerConstants.CodeActions.ExtractToCodeBehindAction;

    public async Task<WorkspaceEdit?> ResolveAsync(DocumentContext documentContext, JsonElement data, RazorFormattingOptions options, CancellationToken cancellationToken)
    {
        var actionParams = data.Deserialize<ExtractToCodeBehindCodeActionParams>();
        if (actionParams is null)
        {
            return null;
        }

        var path = FilePathNormalizer.Normalize(documentContext.Uri.GetAbsoluteOrUNCPath());

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        if (codeDocument.IsUnsupported())
        {
            return null;
        }

        if (!FileKinds.IsComponent(codeDocument.GetFileKind()))
        {
            return null;
        }

        var codeBehindPath = FileUtilities.GenerateUniquePath(path, $"{Path.GetExtension(path)}.cs");

        // VS Code in Windows expects path to start with '/'
        var updatedCodeBehindPath = _languageServerFeatureOptions.ReturnCodeActionAndRenamePathsWithPrefixedSlash && !codeBehindPath.StartsWith("/")
            ? '/' + codeBehindPath
            : codeBehindPath;

        var codeBehindUri = VsLspFactory.CreateFilePathUri(updatedCodeBehindPath);

        var text = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);

        var className = Path.GetFileNameWithoutExtension(path);
        var codeBlockContent = text.GetSubTextString(new TextSpan(actionParams.ExtractStart, actionParams.ExtractEnd - actionParams.ExtractStart)).Trim();
        var codeBehindContent = GenerateCodeBehindClass(className, actionParams.Namespace, codeBlockContent, codeDocument);

        codeBehindContent = await _roslynCodeActionHelpers.GetFormattedNewFileContentsAsync(documentContext.Project, codeBehindUri, codeBehindContent, cancellationToken).ConfigureAwait(false);

        var removeRange = codeDocument.Source.Text.GetRange(actionParams.RemoveStart, actionParams.RemoveEnd);

        var codeDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier { Uri = documentContext.Uri };
        var codeBehindDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier { Uri = codeBehindUri };

        var documentChanges = new SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[]
        {
            new CreateFile { Uri = codeBehindUri },
            new TextDocumentEdit
            {
                TextDocument = codeDocumentIdentifier,
                Edits = [VsLspFactory.CreateTextEdit(removeRange, string.Empty)]
            },
            new TextDocumentEdit
            {
                TextDocument = codeBehindDocumentIdentifier,
                Edits = [VsLspFactory.CreateTextEdit(position: (0, 0), codeBehindContent)]
            }
        };

        return new WorkspaceEdit
        {
            DocumentChanges = documentChanges,
        };
    }

    private string GenerateCodeBehindClass(string className, string namespaceName, string contents, RazorCodeDocument razorCodeDocument)
    {
        using var _ = StringBuilderPool.GetPooledObject(out var builder);

        var usingDirectives = razorCodeDocument
            .GetDocumentIntermediateNode()
            .FindDescendantNodes<UsingDirectiveIntermediateNode>();

        foreach (var usingDirective in usingDirectives)
        {
            builder.Append("using ");

            var content = usingDirective.Content;
            var startIndex = content.StartsWith("global::", StringComparison.Ordinal)
                ? 8
                : 0;

            builder.Append(content, startIndex, content.Length - startIndex);
            builder.Append(';');
            builder.AppendLine();
        }

        builder.AppendLine();
        builder.Append("namespace ");
        builder.AppendLine(namespaceName);
        builder.Append('{');
        builder.AppendLine();
        builder.Append("public partial class ");
        builder.AppendLine(className);
        builder.AppendLine(contents);
        builder.Append('}');

        return builder.ToString();
    }
}
