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
            // PERF: It's intentional that we call _filePathToDocumentMap.ContainsKey(...)
            // before State.Documents.ContainsKey(...), even though the latter check is
            // enough to return the correct answer. This is because _filePathToDocumentMap is
            // a Dictionary<,>, which has O(1) lookup, and State.Documents is an
            // ImmutableDictionary<,>, which has O(log n) lookup. So, checking _filePathToDocumentMap
            // first is faster if the DocumentSnapshot has already been created.

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
            // Have we already seen this document? If so, return it!
            if (_filePathToDocumentMap.TryGetValue(filePath, out var snapshot))
            {
                document = snapshot;
                return true;
            }

            // Do we have DocumentSate for this document? If not, we're done!
            if (!State.Documents.TryGetValue(filePath, out var state))
            {
                document = null;
                return false;
            }

            // If we have DocumentState, go ahead and create a new DocumentSnapshot.
            snapshot = new DocumentSnapshot(this, state);
            _filePathToDocumentMap.Add(filePath, snapshot);

            document = snapshot;
            return true;
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
