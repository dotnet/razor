// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Range = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

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

        var text = componentDocument.Source.Text;
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

        // Right now this includes all the usings in the original document.
        // https://github.com/dotnet/razor/issues/11025 tracks reducing to only the required set.
        var usingDirectives = syntaxTree.GetUsingDirectives();
        foreach (var usingDirective in usingDirectives)
        {
            builder.AppendLine(usingDirective.ToFullString());
        }

        // If any using directives were added, add a newline before the extracted content.
        if (usingDirectives.Length > 0)
        {
            builder.AppendLine();
        }

        var indentation = GetIndentation(actionParams.Start, text);
        var extractedText = text.GetSubTextString(TextSpan.FromBounds(actionParams.Start, actionParams.End));
        var lines = extractedText.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = UnindentLine(lines[i], indentation);
            if (i == (lines.Length - 1))
            {
                builder.Append(line);
            }
            else
            {
                builder.Append(line + '\n');
            }
        }

        var removeRange = text.GetRange(actionParams.Start, actionParams.End);

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
                        Range = VsLspFactory.DefaultRange,
                    }
                ],
            }
        };

        return new WorkspaceEdit
        {
            DocumentChanges = documentChanges,
        };
    }

    private string UnindentLine(string line, int indentation)
    {
        var startCharacter = 0;

        // Keep passing characters until either we reach the root indendation level
        // or we would consume a character that isn't whitespace. This does make assumptions
        // about consistency of tabs or spaces but at least will only fail to unindent correctly
        while (startCharacter < indentation && IsWhitespace(line[startCharacter]))
        {
            startCharacter++;
        }

        return line[startCharacter..];
    }

    private int GetIndentation(int start, SourceText text)
    {
        var dedent = 0;
        while (IsWhitespace(text[--start]))
        {
            dedent++;
        }

        return dedent;
    }

    private static bool IsWhitespace(char c)
        => c == ' ' || c == '\t';
}
