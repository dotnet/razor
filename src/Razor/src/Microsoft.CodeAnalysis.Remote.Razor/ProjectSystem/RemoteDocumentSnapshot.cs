// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

internal sealed class RemoteDocumentSnapshot : IDocumentSnapshot
{
    public TextDocument TextDocument { get; }
    public RemoteProjectSnapshot ProjectSnapshot { get; }

    // TODO: Delete this field when the source generator is hooked up
    private Document? _generatedDocument;

    private RazorCodeDocument? _codeDocument;

    public RemoteDocumentSnapshot(TextDocument textDocument, RemoteProjectSnapshot projectSnapshot)
    {
        if (!textDocument.IsRazorDocument())
        {
            throw new ArgumentException(SR.Document_is_not_a_Razor_document);
        }

        TextDocument = textDocument;
        ProjectSnapshot = projectSnapshot;
    }

    public string FileKind => FileKinds.GetFileKindFromFilePath(FilePath);
    public string FilePath => TextDocument.FilePath.AssumeNotNull();
    public string TargetPath => TextDocument.FilePath.AssumeNotNull();

    public IProjectSnapshot Project => ProjectSnapshot;

    public int Version => -999; // We don't expect to use this in cohosting, but plenty of existing code logs it's value

    public ValueTask<SourceText> GetTextAsync(CancellationToken cancellationToken)
    {
        return TryGetText(out var result)
            ? new(result)
            : new(TextDocument.GetTextAsync(cancellationToken));
    }

    public ValueTask<VersionStamp> GetTextVersionAsync(CancellationToken cancellationToken)
    {
        return TryGetTextVersion(out var result)
            ? new(result)
            : new(TextDocument.GetTextVersionAsync(cancellationToken));
    }

    public bool TryGetText([NotNullWhen(true)] out SourceText? result)
        => TextDocument.TryGetText(out result);

    public bool TryGetTextVersion(out VersionStamp result)
        => TextDocument.TryGetTextVersion(out result);

    public async Task<RazorCodeDocument> GetGeneratedOutputAsync(bool forceDesignTimeGeneratedOutput)
    {
        // TODO: We don't need to worry about locking if we get called from the didOpen/didChange LSP requests, as CLaSP
        //       takes care of that for us, and blocks requests until those are complete. If that doesn't end up happening,
        //       then a locking mechanism here would prevent concurrent compilations.
        if (TryGetGeneratedOutput(out var codeDocument))
        {
            return codeDocument;
        }

        // The non-cohosted DocumentSnapshot implementation uses DocumentState to get the generated output, and we could do that too
        // but most of that code is optimized around caching pre-computed results when things change that don't affect the compilation.
        // We can't do that here because we are using Roslyn's project snapshots, which don't contain the info that Razor needs. We could
        // in future provide a side-car mechanism so we can cache things, but still take advantage of snapshots etc. but the working
        // assumption for this code is that the source generator will be used, and it will do all of that, so this implementation is naive
        // and simply compiles when asked, and if a new document snapshot comes in, we compile again. This is presumably worse for perf
        // but since we don't expect users to ever use cohosting without source generators, it's fine for now.

        var projectEngine = await ProjectSnapshot.GetProjectEngine_CohostOnlyAsync(CancellationToken.None).ConfigureAwait(false);
        var tagHelpers = await ProjectSnapshot.GetTagHelpersAsync(CancellationToken.None).ConfigureAwait(false);
        var imports = await DocumentState.GetImportsAsync(this, projectEngine).ConfigureAwait(false);

        // TODO: Get the configuration for forceRuntimeCodeGeneration
        // var forceRuntimeCodeGeneration = _projectSnapshot.Configuration.LanguageServerFlags?.ForceRuntimeCodeGeneration ?? false;

        codeDocument = await DocumentState
            .GenerateCodeDocumentAsync(this, projectEngine, imports, tagHelpers, forceRuntimeCodeGeneration: false)
            .ConfigureAwait(false);

        return _codeDocument ??= InterlockedOperations.Initialize(ref _codeDocument, codeDocument);
    }

    public IDocumentSnapshot WithText(SourceText text)
    {
        var id = TextDocument.Id;
        var newDocument = TextDocument.Project.Solution
            .WithAdditionalDocumentText(id, text)
            .GetAdditionalDocument(id)
            .AssumeNotNull();

        var snapshotManager = ProjectSnapshot.SolutionSnapshot.SnapshotManager;
        return snapshotManager.GetSnapshot(newDocument);
    }

    public bool TryGetGeneratedOutput([NotNullWhen(true)] out RazorCodeDocument? result)
    {
        result = _codeDocument;
        return result is not null;
    }

    public async Task<Document> GetGeneratedDocumentAsync()
    {
        if (_generatedDocument is Document generatedDocument)
        {
            return generatedDocument;
        }

        generatedDocument = await HACK_GenerateDocumentAsync().ConfigureAwait(false);
        return _generatedDocument ??= InterlockedOperations.Initialize(ref _generatedDocument, generatedDocument);
    }

    private async Task<Document> HACK_GenerateDocumentAsync()
    {
        // TODO: A real implementation needs to get the SourceGeneratedDocument from the solution

        var solution = TextDocument.Project.Solution;
        var filePathService = ProjectSnapshot.SolutionSnapshot.SnapshotManager.FilePathService;
        var generatedFilePath = filePathService.GetRazorCSharpFilePath(Project.Key, FilePath.AssumeNotNull());
        var projectId = TextDocument.Project.Id;
        var generatedDocumentId = solution.GetDocumentIdsWithFilePath(generatedFilePath).First(d => d.ProjectId == projectId);
        var generatedDocument = solution.GetRequiredDocument(generatedDocumentId);

        var codeDocument = await this.GetGeneratedOutputAsync().ConfigureAwait(false);
        var csharpSourceText = codeDocument.GetCSharpSourceText();

        // HACK: We're not in the same solution fork as the LSP server that provides content for this document
        return generatedDocument.WithText(csharpSourceText);
    }

    public async Task<SyntaxTree> GetCSharpSyntaxTreeAsync(CancellationToken cancellationToken)
    {
        var document = await GetGeneratedDocumentAsync().ConfigureAwait(false);
        var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        return tree.AssumeNotNull();
    }
}
