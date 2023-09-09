// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

internal sealed class ExtractToCodeBehindCodeActionResolver : IRazorCodeActionResolver
{
    private readonly DocumentContextFactory _documentContextFactory;
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;
    private readonly ProjectSnapshotManagerAccessor _projectSnapshotManagerAccessor;

    public ExtractToCodeBehindCodeActionResolver(
        DocumentContextFactory documentContextFactory,
        LanguageServerFeatureOptions languageServerFeatureOptions,
        ProjectSnapshotManagerAccessor projectSnapshotManagerAccessor)
    {
        _documentContextFactory = documentContextFactory ?? throw new ArgumentNullException(nameof(documentContextFactory));
        _languageServerFeatureOptions = languageServerFeatureOptions;
        _projectSnapshotManagerAccessor = projectSnapshotManagerAccessor;
    }

    public string Action => LanguageServerConstants.CodeActions.ExtractToCodeBehindAction;

    public async Task<WorkspaceEdit?> ResolveAsync(JObject data, CancellationToken cancellationToken)
    {
        if (data is null)
        {
            return null;
        }

        var actionParams = data.ToObject<ExtractToCodeBehindCodeActionParams>();
        if (actionParams is null)
        {
            return null;
        }

        var path = FilePathNormalizer.Normalize(actionParams.Uri.GetAbsoluteOrUNCPath());

        var documentContext = _documentContextFactory.TryCreate(actionParams.Uri);
        if (documentContext is null)
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

        var codeBehindPath = GenerateCodeBehindPath(path);

        // VS Code in Windows expects path to start with '/'
        var updatedCodeBehindPath = _languageServerFeatureOptions.ReturnCodeActionAndRenamePathsWithPrefixedSlash && !codeBehindPath.StartsWith("/")
            ? '/' + codeBehindPath
            : codeBehindPath;

        var codeBehindUri = new UriBuilder
        {
            Scheme = Uri.UriSchemeFile,
            Path = updatedCodeBehindPath,
            Host = string.Empty,
        }.Uri;

        var text = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        if (text is null)
        {
            return null;
        }

        var className = Path.GetFileNameWithoutExtension(path);
        var codeBlockContent = text.GetSubTextString(new CodeAnalysis.Text.TextSpan(actionParams.ExtractStart, actionParams.ExtractEnd - actionParams.ExtractStart));
        var codeBehindContent = GenerateCodeBehindClass(className, actionParams.Namespace, codeBlockContent, codeDocument);

        var start = codeDocument.Source.Lines.GetLocation(actionParams.RemoveStart);
        var end = codeDocument.Source.Lines.GetLocation(actionParams.RemoveEnd);
        var removeRange = new Range
        {
            Start = new Position(start.LineIndex, start.CharacterIndex),
            End = new Position(end.LineIndex, end.CharacterIndex)
        };

        var codeDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier { Uri = actionParams.Uri };
        var codeBehindDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier { Uri = codeBehindUri };

        var documentChanges = new SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[]
        {
            new CreateFile { Uri = codeBehindUri },
            new TextDocumentEdit
            {
                TextDocument = codeDocumentIdentifier,
                Edits = new[]
                {
                    new TextEdit
                    {
                        NewText = string.Empty,
                        Range = removeRange,
                    }
                },
            },
            new TextDocumentEdit
            {
                TextDocument = codeBehindDocumentIdentifier,
                Edits  = new[]
                {
                    new TextEdit
                    {
                        NewText = codeBehindContent,
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
    /// correct codebehind extension, using numbers to differentiate from
    /// any collisions.
    /// </summary>
    /// <param name="path">The origin file path.</param>
    /// <returns>A non-existent file path with the same base name and a codebehind extension.</returns>
    private static string GenerateCodeBehindPath(string path)
    {
        var n = 0;
        string codeBehindPath;
        do
        {
            var identifier = n > 0 ? n.ToString(CultureInfo.InvariantCulture) : string.Empty;  // Make it look nice
            var directoryName = Path.GetDirectoryName(path);
            Assumes.NotNull(directoryName);

            codeBehindPath = Path.Combine(
                directoryName,
                $"{Path.GetFileNameWithoutExtension(path)}{identifier}{Path.GetExtension(path)}.cs");
            n++;
        }
        while (File.Exists(codeBehindPath));

        return codeBehindPath;
    }

    /// <summary>
    /// Generate a complete C# compilation unit containing a partial class
    /// with the given name, body contents, and the namespace and all
    /// usings from the existing code document.
    /// </summary>
    /// <param name="className">Name of the resultant partial class.</param>
    /// <param name="namespaceName">Name of the namespace to put the resultant class in.</param>
    /// <param name="contents">Class body contents.</param>
    /// <param name="razorCodeDocument">Existing code document we're extracting from.</param>
    /// <returns></returns>
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
        builder.Append("    public partial class ");
        builder.AppendLine(className);
        builder.Append("    ");
        builder.AppendLine(contents);
        builder.Append('}');

        // TODO: Rather than format here, call Roslyn via LSP to format, and remove and sort usings: https://github.com/dotnet/razor/issues/8766
        var node = CSharpSyntaxTree.ParseText(builder.ToString()).GetRoot();
        node = Formatter.Format(node, _projectSnapshotManagerAccessor.Instance.Workspace);

        return node.ToFullString();
    }
}
