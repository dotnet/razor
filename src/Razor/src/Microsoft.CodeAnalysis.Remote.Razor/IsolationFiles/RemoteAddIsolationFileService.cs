// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.CodeActions;
using Microsoft.CodeAnalysis.Razor.IsolationFiles;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RemoteAddIsolationFileService(in ServiceArgs args)
    : RazorDocumentServiceBase(in args), IRemoteAddIsolationFileService
{
    internal sealed class Factory : FactoryBase<IRemoteAddIsolationFileService>
    {
        protected override IRemoteAddIsolationFileService CreateService(in ServiceArgs args)
            => new RemoteAddIsolationFileService(in args);
    }

    private readonly IRoslynCodeActionHelpers _roslynCodeActionHelpers =
        args.ExportProvider.GetExportedValue<IRoslynCodeActionHelpers>();

    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions =
        args.ExportProvider.GetExportedValue<LanguageServerFeatureOptions>();

    public ValueTask<WorkspaceEdit?> AddIsolationFileAsync(
        JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo,
        Uri razorFileUri,
        string fileKind,
        CancellationToken cancellationToken)
        => RunServiceAsync(
            solutionInfo,
            solution => AddIsolationFileAsync(solution, razorFileUri, fileKind, cancellationToken),
            cancellationToken);

    private async ValueTask<WorkspaceEdit?> AddIsolationFileAsync(
        Solution solution,
        Uri razorFileUri,
        string fileKind,
        CancellationToken cancellationToken)
    {
        if (!solution.TryGetRazorDocument(razorFileUri, out var razorDocument))
        {
            Logger.LogWarning($"Could not find Razor document for URI: {razorFileUri}");
            return null;
        }

        var documentContext = CreateRazorDocumentContext(solution, razorDocument.Id);
        if (documentContext is null)
        {
            Logger.LogWarning($"Could not create document context for: {razorFileUri}");
            return null;
        }

        var razorFilePath = FilePathNormalizer.Normalize(razorFileUri.GetAbsoluteOrUNCPath());
        var isolationFilePath = GetIsolationFilePath(razorFilePath, fileKind);
        if (isolationFilePath is null)
        {
            return null;
        }

        var isolationFileUri = LspFactory.CreateFilePathUri(isolationFilePath, _languageServerFeatureOptions);

        var content = await GenerateContentAsync(
            fileKind, documentContext, razorFilePath, isolationFileUri, cancellationToken).ConfigureAwait(false);

        var isolationFileDocumentIdentifier = new OptionalVersionedTextDocumentIdentifier
        {
            DocumentUri = new DocumentUri(isolationFileUri)
        };

        var documentChanges = new SumType<TextDocumentEdit, CreateFile, RenameFile, DeleteFile>[]
        {
            new CreateFile { DocumentUri = isolationFileDocumentIdentifier.DocumentUri },
            new TextDocumentEdit
            {
                TextDocument = isolationFileDocumentIdentifier,
                Edits = [LspFactory.CreateTextEdit(position: (0, 0), content)]
            }
        };

        return new WorkspaceEdit
        {
            DocumentChanges = documentChanges,
        };
    }

    private static string? GetIsolationFilePath(string razorFilePath, string fileKind)
    {
        return fileKind switch
        {
            IsolationFileKind.Css => razorFilePath + ".css",
            IsolationFileKind.CSharp => razorFilePath + ".cs",
            IsolationFileKind.JavaScript => razorFilePath + ".js",
            _ => null
        };
    }

    private async Task<string> GenerateContentAsync(
        string fileKind,
        RemoteDocumentContext documentContext,
        string razorFilePath,
        Uri isolationFileUri,
        CancellationToken cancellationToken)
    {
        return fileKind switch
        {
            IsolationFileKind.CSharp => await GenerateCSharpContentAsync(
                documentContext, razorFilePath, isolationFileUri, cancellationToken).ConfigureAwait(false),
            IsolationFileKind.Css => GenerateCssContent(razorFilePath),
            IsolationFileKind.JavaScript => GenerateJavaScriptContent(razorFilePath),
            _ => string.Empty
        };
    }

    private static string GenerateCssContent(string razorFilePath)
    {
        var componentName = Path.GetFileNameWithoutExtension(razorFilePath);
        var fileType = FileKinds.GetFileKindFromPath(razorFilePath).IsComponent() ? "component" : "view";
        return $"/* Scoped CSS styles for {componentName} {fileType} */\r\n";
    }

    private static string GenerateJavaScriptContent(string razorFilePath)
    {
        var componentName = Path.GetFileNameWithoutExtension(razorFilePath);
        return $"// JavaScript isolation for {componentName} component\r\n";
    }

    private async Task<string> GenerateCSharpContentAsync(
        RemoteDocumentContext documentContext,
        string razorFilePath,
        Uri isolationFileUri,
        CancellationToken cancellationToken)
    {
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var className = Path.GetFileNameWithoutExtension(razorFilePath);

        // Use the Razor compiler's namespace resolution which handles @namespace directives,
        // _Imports.razor, and SDK-provided root namespace
        if (!codeDocument.TryGetNamespace(fallbackToRootNamespace: true, out var @namespace))
        {
            Logger.LogWarning($"Could not determine namespace for: {razorFilePath}");
            @namespace = "Unknown";
        }

        var content = GenerateCodeBehindClass(className, @namespace, codeDocument);

        // Format via Roslyn (handles file-scoped namespaces, indentation, etc.)
        content = await _roslynCodeActionHelpers.GetFormattedNewFileContentsAsync(
            documentContext.Snapshot.Project,
            isolationFileUri,
            content,
            cancellationToken).ConfigureAwait(false);

        return content;
    }

    private static string GenerateCodeBehindClass(string className, string namespaceName, RazorCodeDocument razorCodeDocument)
    {
        using var _ = StringBuilderPool.GetPooledObject(out var builder);

        var usingDirectives = razorCodeDocument
            .GetRequiredDocumentNode()
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
        builder.Append('{');
        builder.AppendLine();
        builder.Append('}');
        builder.AppendLine();
        builder.Append('}');

        return builder.ToString();
    }
}
