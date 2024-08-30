// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

internal class RemoteDocumentSnapshot(TextDocument textDocument, RemoteProjectSnapshot projectSnapshot, IFilePathService filePathService) : IDocumentSnapshot
{
    private readonly TextDocument _textDocument = textDocument;
    private readonly RemoteProjectSnapshot _projectSnapshot = projectSnapshot;
    private readonly IFilePathService _filePathService = filePathService;

    // TODO: Delete this field when the source generator is hooked up
    private Document? _generatedDocument;

    private RazorCodeDocument? _codeDocument;

    public TextDocument TextDocument => _textDocument;

    public string? FileKind => FileKinds.GetFileKindFromFilePath(FilePath);

    public string? FilePath => _textDocument.FilePath;

    public string? TargetPath => _textDocument.FilePath;

    public IProjectSnapshot Project => _projectSnapshot;

    public bool SupportsOutput => true;

    public int Version => -999; // We don't expect to use this in cohosting, but plenty of existing code logs it's value

    public Task<SourceText> GetTextAsync() => _textDocument.GetTextAsync();

    public Task<VersionStamp> GetTextVersionAsync() => _textDocument.GetTextVersionAsync();

    public bool TryGetText([NotNullWhen(true)] out SourceText? result) => _textDocument.TryGetText(out result);

    public bool TryGetTextVersion(out VersionStamp result) => _textDocument.TryGetTextVersion(out result);

    public async Task<RazorCodeDocument> GetGeneratedOutputAsync(bool _)
    {
        // TODO: We don't need to worry about locking if we get called from the didOpen/didChange LSP requests, as CLaSP
        //       takes care of that for us, and blocks requests until those are complete. If that doesn't end up happening,
        //       then a locking mechanism here would prevent concurrent compilations.
        if (TryGetGeneratedOutput(out var codeDocument))
        {
            return codeDocument;
        }

        codeDocument = await _projectSnapshot.GetCodeDocumentAsync(this, cancellationToken: CancellationToken.None);
        return InterlockedOperations.Initialize(ref _codeDocument, codeDocument.AssumeNotNull());
    }

    public IDocumentSnapshot WithText(SourceText text)
    {
        var id = _textDocument.Id;
        var newDocument = _textDocument.Project.Solution.WithAdditionalDocumentText(id, text).GetAdditionalDocument(id).AssumeNotNull();

        return new RemoteDocumentSnapshot(newDocument, _projectSnapshot, _filePathService);
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

        generatedDocument = await _projectSnapshot.GetGeneratedDocumentAsync(this, cancellationToken: CancellationToken.None);
        return InterlockedOperations.Initialize(ref _generatedDocument, generatedDocument);
    }

    public async Task<SyntaxTree> GetCSharpSyntaxTreeAsync(CancellationToken cancellationToken)
    {
        var document = await GetGeneratedDocumentAsync().ConfigureAwait(false);
        var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        return tree.AssumeNotNull();
    }
}
