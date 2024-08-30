// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.NET.Sdk.Razor.SourceGenerators;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

#pragma warning disable RSEXPERIMENTAL004 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
internal class RemoteProjectSnapshot : IProjectSnapshot
{
    public ProjectKey Key { get; }

    private readonly Project _project;
    private readonly DocumentSnapshotFactory _documentSnapshotFactory;
    private readonly ITelemetryReporter _telemetryReporter;
    private readonly Dictionary<string, ImmutableArray<string>> _importsToRelatedDocuments = new();

    private ImmutableArray<TagHelperDescriptor> _tagHelpers;

    public RemoteProjectSnapshot(Project project, DocumentSnapshotFactory documentSnapshotFactory, ITelemetryReporter telemetryReporter)
    {
        _project = project;
        _documentSnapshotFactory = documentSnapshotFactory;
        _telemetryReporter = telemetryReporter;
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
            var runResult = await GetRazorRunResultAsync(cancellationToken);
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

    internal async Task<RazorCodeDocument?> GetCodeDocumentAsync(IDocumentSnapshot documentSnapshot, CancellationToken cancellationToken)
    {
        var runResult = await GetRazorRunResultAsync(cancellationToken);
        if (runResult is null)
        {
            // There was no generator, so we couldn't get anything from it
            return null;
        }

        var relativePath = GetDocumentRelativePath(documentSnapshot);
        if (!runResult.Value.HostOutputs.TryGetValue(relativePath, out var objectCodeDocument) || objectCodeDocument is not RazorCodeDocument codeDocument)
        {
            return null;
        }

        var targetPath = documentSnapshot.TargetPath.AssumeNotNull();
        if (!_importsToRelatedDocuments.ContainsKey(targetPath))
        {
            lock (_importsToRelatedDocuments)
            {
                if (!_importsToRelatedDocuments.ContainsKey(targetPath))
                {
                    _importsToRelatedDocuments[documentSnapshot.TargetPath!] = codeDocument.Imports.SelectAsArray(i => i.FilePath!);
                }
            }
        }

        return codeDocument;
    }

    private async Task<GeneratorRunResult?> GetRazorRunResultAsync(CancellationToken cancellationToken)
    {
        var result = await _project.GetSourceGeneratorRunResultAsync(cancellationToken);
        return result?.Results.SingleOrDefault(r => r.Generator.GetGeneratorType().Name == typeof(RazorSourceGenerator).Name);
    }

    internal async Task<Document> GetGeneratedDocumentAsync(IDocumentSnapshot documentSnapshot, CancellationToken cancellationToken)
    {
        // TODO: we should filter the documents by generator to make sure its actually ours.
        // That info isn't public in Roslyn but we can create an EA method to do it.
        var generatedDocuments = await _project.GetSourceGeneratedDocumentsAsync(cancellationToken);
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

    public ImmutableArray<IDocumentSnapshot> GetRelatedDocuments(IDocumentSnapshot document)
    {
        var targetPath = document.TargetPath.AssumeNotNull();

        if (!_importsToRelatedDocuments.TryGetValue(targetPath, out var relatedDocuments))
        {
            return [];
        }

        using var builder = new PooledArrayBuilder<IDocumentSnapshot>(relatedDocuments.Length);

        foreach (var relatedDocumentFilePath in relatedDocuments)
        {
            if (TryGetDocument(relatedDocumentFilePath, out var relatedDocument))
            {
                builder.Add(relatedDocument);
            }
        }

        return builder.DrainToImmutable();
    }

    public bool IsImportDocument(IDocumentSnapshot document)
    {
        return document.TargetPath is { } targetPath &&
               _importsToRelatedDocuments.ContainsKey(targetPath);
    }
}
