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
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;
using Microsoft.VisualStudio.Utilities;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.Formatting;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;

internal sealed class ExtractToComponentCodeActionResolver(
    IDocumentContextFactory documentContextFactory,
    LanguageServerFeatureOptions languageServerFeatureOptions) : IRazorCodeActionResolver
{
    private readonly IDocumentContextFactory _documentContextFactory = documentContextFactory;
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions;

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

        var text = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (text is null)
        {
            return null;
        }

        var path = FilePathNormalizer.Normalize(actionParams.Uri.GetAbsoluteOrUNCPath());
        var directoryName = Path.GetDirectoryName(path).AssumeNotNull();
        var templatePath = Path.Combine(directoryName, "Component.razor");
        var componentPath = FileUtilities.GenerateUniquePath(templatePath, ".razor");
        var componentName = Path.GetFileNameWithoutExtension(componentPath);

        // VS Code in Windows expects path to start with '/'
        componentPath = _languageServerFeatureOptions.ReturnCodeActionAndRenamePathsWithPrefixedSlash && !componentPath.StartsWith('/')
            ? '/' + componentPath
            : componentPath;

        var newComponentUri = new UriBuilder
        {
            Scheme = Uri.UriSchemeFile,
            Path = componentPath,
            Host = string.Empty,
        }.Uri;

        using var _ = StringBuilderPool.GetPooledObject(out var builder);

        var syntaxTree = componentDocument.GetSyntaxTree();
        foreach (var usingDirective in GetUsingsInDocument(syntaxTree))
        {
            builder.AppendLine(usingDirective);
        }

        var extractedText = text.GetSubTextString(new TextSpan(actionParams.Start, actionParams.End - actionParams.Start)).Trim();
        builder.Append(extractedText);

        var start = componentDocument.Source.Text.Lines.GetLinePosition(actionParams.Start);
        var end = componentDocument.Source.Text.Lines.GetLinePosition(actionParams.End);
        var removeRange = new Range
        {
            Start = new Position(start.Line, start.Character),
            End = new Position(end.Line, end.Character)
        };

        var documentChanges = new SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[]
        {
            new CreateFile { Uri = newComponentUri },
            new TextDocumentEdit
            {
                TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = actionParams.Uri },
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
                TextDocument = new OptionalVersionedTextDocumentIdentifier { Uri = newComponentUri },
                Edits  =
                [
                    new TextEdit
                    {
                        NewText = builder.ToString(),
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

    private static IEnumerable<string> GetUsingsInDocument(RazorSyntaxTree syntaxTree)
        => syntaxTree
            .Root
            .ChildNodes()
            .Select(node =>
            {
                if (node.IsUsingDirective(out var _))
                {
                    return node.ToFullString().Trim();
                }

                return null;
            })
            .WhereNotNull();
}
