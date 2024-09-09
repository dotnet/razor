// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.NET.Sdk.Razor.SourceGenerators;

namespace Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

#pragma warning disable RSEXPERIMENTAL004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal class RemoteProjectSnapshot : IProjectSnapshot
{
    public ProjectKey Key { get; }

    private readonly Project _project;
    private readonly DocumentSnapshotFactory _documentSnapshotFactory;

    private ImmutableArray<TagHelperDescriptor> _tagHelpers;

    public RemoteProjectSnapshot(Project project, DocumentSnapshotFactory documentSnapshotFactory)
    {
        _project = project;
        _documentSnapshotFactory = documentSnapshotFactory;
        Key = _project.ToProjectKey();
    }

    public RazorConfiguration Configuration => throw new InvalidOperationException("Should not be called for cohosted projects.");

    public IEnumerable<string> DocumentFilePaths
    {
        get
        {
            foreach (var additionalDocument in _project.AdditionalDocuments)
            {
                if (additionalDocument.FilePath is not string filePath)
                {
                    continue;
                }

                if (!filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) &&
                    !filePath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return filePath;
            }
        }
    }

    public string FilePath => _project.FilePath!;

    public string IntermediateOutputPath => FilePathNormalizer.GetNormalizedDirectoryName(_project.CompilationOutputInfo.AssemblyPath);

    public string? RootNamespace => _project.DefaultNamespace ?? "ASP";

    public string DisplayName => _project.Name;

    public VersionStamp Version => _project.Version;

    public LanguageVersion CSharpLanguageVersion => ((CSharpParseOptions)_project.ParseOptions!).LanguageVersion;

    public async ValueTask<ImmutableArray<TagHelperDescriptor>> GetTagHelpersAsync(CancellationToken cancellationToken)
    {
        if (_tagHelpers.IsDefault)
        {
            var runResult = await _project.GetRazorGeneratorRunResultAsync(cancellationToken).ConfigureAwait(false);
            var computedTagHelpers = (ImmutableArray<TagHelperDescriptor>)runResult.Value.HostOutputs["TagHelpers"];
            ImmutableInterlocked.InterlockedInitialize(ref _tagHelpers, computedTagHelpers);
        }

        return _tagHelpers;
    }

    public ProjectWorkspaceState ProjectWorkspaceState => throw new InvalidOperationException("Should not be called for cohosted projects.");

    public IDocumentSnapshot? GetDocument(string filePath)
    {
        var textDocument = _project.AdditionalDocuments.FirstOrDefault(d => d.FilePath == filePath);
        if (textDocument is null)
        {
            return null;
        }

        return _documentSnapshotFactory.GetOrCreate(textDocument);
    }

    public bool TryGetDocument(string filePath, [NotNullWhen(true)] out IDocumentSnapshot? document)
    {
        document = GetDocument(filePath);
        return document is not null;
    }

    public RazorProjectEngine GetProjectEngine() => throw new InvalidOperationException("Should not be called for cohosted projects.");

    internal Task<RazorCodeDocument?> GetCodeDocumentAsync(IDocumentSnapshot documentSnapshot, CancellationToken cancellationToken)
    {
        var relativePath = GetDocumentRelativePath(documentSnapshot);

        return _project.TryGetGeneratedRazorCodeDocumentAsync(relativePath, cancellationToken);
    }

    internal async Task<Document> GetGeneratedDocumentAsync(IDocumentSnapshot documentSnapshot, CancellationToken cancellationToken)
    {
        // TODO: we should filter the documents by generator to make sure its actually ours.
        // That info isn't public in Roslyn but we can create an EA method to do it.
        var generatedDocuments = await _project.GetSourceGeneratedDocumentsAsync(cancellationToken).ConfigureAwait(false);
        var relativePath = GetDocumentRelativePath(documentSnapshot);
        var generatedIdentifier = RazorSourceGenerator.GetIdentifierFromPath(relativePath);
        return generatedDocuments.Single(d => d.HintName == generatedIdentifier); 
    }

    private string GetDocumentRelativePath(IDocumentSnapshot documentSnapshot)
    {
        var projectRoot = Path.GetDirectoryName(_project.FilePath);
        Debug.Assert(documentSnapshot.TargetPath!.StartsWith(projectRoot));
        return documentSnapshot.TargetPath[(projectRoot.Length + 1)..];
    }
}
