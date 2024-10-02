// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal class ProjectSnapshot(ProjectState state) : IProjectSnapshot
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DocumentSnapshot> _filePathToDocumentMap = new(FilePathNormalizingComparer.Instance);

    public ProjectKey Key => State.HostProject.Key;

    public ProjectState State { get; } = state;

    public RazorConfiguration Configuration => HostProject.Configuration;

    public IEnumerable<string> DocumentFilePaths => State.Documents.Keys;

    public int DocumentCount => State.Documents.Count;

    public string FilePath => State.HostProject.FilePath;

    public string IntermediateOutputPath => State.HostProject.IntermediateOutputPath;

    public string? RootNamespace => State.HostProject.RootNamespace;

    public string DisplayName => State.HostProject.DisplayName;

    public LanguageVersion CSharpLanguageVersion => State.CSharpLanguageVersion;

    public HostProject HostProject => State.HostProject;

    public virtual VersionStamp Version => State.Version;

    public ValueTask<ImmutableArray<TagHelperDescriptor>> GetTagHelpersAsync(CancellationToken cancellationToken) => new(State.TagHelpers);

    public ProjectWorkspaceState ProjectWorkspaceState => State.ProjectWorkspaceState;

    public bool ContainsDocument(string filePath)
    {
        lock (_gate)
        {
            return _filePathToDocumentMap.ContainsKey(filePath) ||
                   State.Documents.ContainsKey(filePath);
        }
    }

    public IDocumentSnapshot? GetDocument(string filePath)
        => TryGetDocument(filePath, out var document)
            ? document
            : null;

    public bool TryGetDocument(string filePath, [NotNullWhen(true)] out IDocumentSnapshot? document)
    {
        lock (_gate)
        {
            // We only create a new DocumentSnapshot if we haven't created one yet
            // but have DocumentState for it.
            if (!_filePathToDocumentMap.TryGetValue(filePath, out var snapshot) &&
                State.Documents.TryGetValue(filePath, out var state))
            {
                snapshot = new DocumentSnapshot(this, state);
                _filePathToDocumentMap.Add(filePath, snapshot);
            }

            document = snapshot;
            return document is not null;
        }
    }

    /// <summary>
    /// If the provided document is an import document, gets the other documents in the project
    /// that include directives specified by the provided document. Otherwise returns an empty
    /// list.
    /// </summary>
    public ImmutableArray<IDocumentSnapshot> GetRelatedDocuments(IDocumentSnapshot document)
    {
        var targetPath = document.TargetPath.AssumeNotNull();

        if (!State.ImportsToRelatedDocuments.TryGetValue(targetPath, out var relatedDocuments))
        {
            return [];
        }

        lock (_gate)
        {
            using var builder = new PooledArrayBuilder<IDocumentSnapshot>(capacity: relatedDocuments.Length);

            foreach (var relatedDocumentFilePath in relatedDocuments)
            {
                if (TryGetDocument(relatedDocumentFilePath, out var relatedDocument))
                {
                    builder.Add(relatedDocument);
                }
            }

            return builder.DrainToImmutable();
        }
    }

    public virtual RazorProjectEngine GetProjectEngine()
    {
        return State.ProjectEngine;
    }
}
