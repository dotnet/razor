// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Cohost;

internal class CohostProjectSnapshot(Project project, DocumentSnapshotFactory documentSnapshotFactory) : IProjectSnapshot
{
    private readonly Project _project = project;
    private readonly DocumentSnapshotFactory _documentSnapshotFactory = documentSnapshotFactory;

    public ProjectKey Key => ProjectKey.From(_project)!.Value;

    public RazorConfiguration? Configuration => throw new NotImplementedException();

    public IEnumerable<string> DocumentFilePaths
        => _project.AdditionalDocuments
            .Where(d => d.FilePath.AssumeNotNull().EndsWith(".razor", StringComparison.OrdinalIgnoreCase) || d.FilePath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.FilePath.AssumeNotNull());

    public string FilePath => _project.FilePath!;

    public string IntermediateOutputPath => FilePathNormalizer.GetNormalizedDirectoryName(_project.CompilationOutputInfo.AssemblyPath);

    public string? RootNamespace => _project.DefaultNamespace;

    public string DisplayName => _project.Name;

    public VersionStamp Version => _project.Version;

    public LanguageVersion CSharpLanguageVersion => ((CSharpParseOptions)_project.ParseOptions!).LanguageVersion;

    public ImmutableArray<TagHelperDescriptor> TagHelpers => throw new NotImplementedException();

    public ProjectWorkspaceState? ProjectWorkspaceState => throw new NotImplementedException();

    public IDocumentSnapshot? GetDocument(string filePath)
    {
        var textDocument = _project.AdditionalDocuments.FirstOrDefault(d => d.FilePath == filePath);
        if (textDocument is null)
        {
            return null;
        }

        return _documentSnapshotFactory.GetOrCreate(textDocument);
    }

    public RazorProjectEngine GetProjectEngine()
    {
        throw new NotImplementedException();
    }

    public ImmutableArray<IDocumentSnapshot> GetRelatedDocuments(IDocumentSnapshot document)
    {
        throw new NotImplementedException();
    }

    public bool IsImportDocument(IDocumentSnapshot document)
    {
        throw new NotImplementedException();
    }
}
