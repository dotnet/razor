// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Text.Json;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Razor;
internal sealed class ExtractToNewComponentCodeActionResolver : IRazorCodeActionResolver
{
    private static readonly Workspace s_workspace = new AdhocWorkspace();

    private readonly IDocumentContextFactory _documentContextFactory;
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;
    private readonly IClientConnection _clientConnection;
    public ExtractToNewComponentCodeActionResolver(
        IDocumentContextFactory documentContextFactory,
        LanguageServerFeatureOptions languageServerFeatureOptions,
        IClientConnection clientConnection)
    {
        _documentContextFactory = documentContextFactory ?? throw new ArgumentNullException(nameof(documentContextFactory));
        _languageServerFeatureOptions = languageServerFeatureOptions ?? throw new ArgumentNullException(nameof(languageServerFeatureOptions));
        _clientConnection = clientConnection ?? throw new ArgumentNullException(nameof(clientConnection));
    }
    public string Action => LanguageServerConstants.CodeActions.ExtractToNewComponentAction;
    public async Task<WorkspaceEdit?> ResolveAsync(JsonElement data, CancellationToken cancellationToken)
    {
        if (data.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        var actionParams = JsonSerializer.Deserialize<ExtractToNewComponentCodeActionParams>(data.GetRawText());

        if (actionParams is null)
        {
            return null;
        }

        var path = FilePathNormalizer.Normalize(actionParams.Uri.GetAbsoluteOrUNCPath());

        if (!_documentContextFactory.TryCreate(actionParams.Uri, out var documentContext))
        {
            return null;
        }

        var componentDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        if (componentDocument.IsUnsupported())
        {
            return null;
        }

        if (!FileKinds.IsComponent(componentDocument.GetFileKind()))
        {
            return null;
        }

        var componentPath = GenerateComponentBehindPath(path);

        // VS Code in Windows expects path to start with '/'
        var updatedComponentPath = _languageServerFeatureOptions.ReturnCodeActionAndRenamePathsWithPrefixedSlash && !componentPath.StartsWith("/")
            ? '/' + componentPath
            : componentPath;

        var newComponentUri = new UriBuilder
        {
            Scheme = Uri.UriSchemeFile,
            Path = updatedComponentPath,
            Host = string.Empty,
        }.Uri;

        var text = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (text is null)
        {
            return null;
        }

        var componentName = Path.GetFileNameWithoutExtension(componentPath);
        var newComponentContent = text.GetSubTextString(new CodeAnalysis.Text.TextSpan(actionParams.ExtractStart, actionParams.ExtractEnd - actionParams.ExtractStart)).Trim();

        var start = componentDocument.Source.Text.Lines.GetLinePosition(actionParams.ExtractStart);
        var end = componentDocument.Source.Text.Lines.GetLinePosition(actionParams.ExtractEnd);
        var removeRange = new Range
        {
            Start = new Position(start.Line, start.Character),
            End = new Position(end.Line, end.Character)
        };

        var componentDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier { Uri = actionParams.Uri };
        var newComponentDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier { Uri = newComponentUri };

        var documentChanges = new SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[]
        {
            new CreateFile { Uri = newComponentUri },
            new TextDocumentEdit
            {
                TextDocument = componentDocumentIdentifier,
                Edits = new[]
                {
                    new TextEdit
                    {
                        NewText = $"<{componentName} />",
                        Range = removeRange,
                    }
                },
            },
            new TextDocumentEdit
            {
                TextDocument = newComponentDocumentIdentifier,
                Edits  = new[]
                {
                    new TextEdit
                    {
                        NewText = newComponentContent,
                        Range = new Range { Start = new Position(0, 0), End = new Position(0, 0) },
                    }
                },
            }
        };

        return new WorkspaceEdit
        {
            DocumentChanges = documentChanges,
        };
    }

    /// <summary>
    /// Generate a file path with adjacent to our input path that has the
    /// correct code-behind extension, using numbers to differentiate from
    /// any collisions.
    /// </summary>
    /// <param name="path">The origin file path.</param>
    /// <returns>A non-existent file path with the same base name and a code-behind extension.</returns>
    private static string GenerateComponentBehindPath(string path)
    {
        var n = 0;
        string componentBehindPath;
        do
        {
            var identifier = n > 0 ? n.ToString(CultureInfo.InvariantCulture) : string.Empty;  // Make it look nice
            var directoryName = Path.GetDirectoryName(path);
            Assumes.NotNull(directoryName);

            componentBehindPath = Path.Combine(
                directoryName,
                $"Component{identifier}.razor");
            n++;
        }
        while (File.Exists(componentBehindPath));

        return componentBehindPath;
    }
}
