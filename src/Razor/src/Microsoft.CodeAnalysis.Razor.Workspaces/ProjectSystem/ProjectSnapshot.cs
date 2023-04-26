// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal class ProjectSnapshot : IProjectSnapshot
{
    private readonly object _lock;

    private readonly Dictionary<string, DocumentSnapshot> _documents;

    public ProjectSnapshot(ProjectState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));

        _lock = new object();
        _documents = new Dictionary<string, DocumentSnapshot>(FilePathComparer.Instance);

        //Microsoft.CodeAnalysis.CodeAnalysisEventSource.Log.Message("Project snapshot created");
    }

    public ProjectState State { get; }

    public RazorConfiguration? Configuration => HostProject.Configuration;

    public IEnumerable<string> DocumentFilePaths => State.Documents.Keys;

    public string FilePath => State.HostProject.FilePath;

    public string? RootNamespace => State.HostProject.RootNamespace;

    public LanguageVersion CSharpLanguageVersion => State.CSharpLanguageVersion;

    public HostProject HostProject => State.HostProject;

    public virtual VersionStamp Version => State.Version;

    public IReadOnlyList<TagHelperDescriptor> TagHelpers => State.TagHelpers;

    public ProjectWorkspaceState? ProjectWorkspaceState => State.ProjectWorkspaceState;

    public virtual IDocumentSnapshot? GetDocument(string filePath)
    {
        lock (_lock)
        {
            if (!_documents.TryGetValue(filePath, out var result) &&
                State.Documents.TryGetValue(filePath, out var state))
            {
                result = new DocumentSnapshot(this, state);
                _documents.Add(filePath, result);
            }

            return result;
        }
    }

    public bool IsImportDocument(IDocumentSnapshot document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        return State.ImportsToRelatedDocuments.ContainsKey(document.TargetPath);
    }

    public ImmutableArray<IDocumentSnapshot> GetRelatedDocuments(IDocumentSnapshot document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var targetPath = document.TargetPath.AssumeNotNull();

        if (!State.ImportsToRelatedDocuments.TryGetValue(targetPath, out var relatedDocuments))
        {
            return ImmutableArray<IDocumentSnapshot>.Empty;
        }

        lock (_lock)
        {
            using var _ = ArrayBuilderPool<IDocumentSnapshot>.GetPooledObject(out var builder);

            foreach (var relatedDocumentFilePath in relatedDocuments)
            {
                if (GetDocument(relatedDocumentFilePath) is { } relatedDocument)
                {
                    builder.Add(relatedDocument);
                }
            }

            return builder.ToImmutableArray();
        }
    }

    public virtual RazorProjectEngine GetProjectEngine()
    {
        return State.ProjectEngine;
    }

    //private GeneratorDriverRunResult? _runResult;

    public async Task<(string CSharp, string Html, string Json)> GetGeneratedDocumentsAsync(IDocumentSnapshot documentSnapshot)
    {
        //Microsoft.CodeAnalysis.CodeAnalysisEventSource.Log.Message("GetCodeDocumentAsync: "+filePath);


        // get all the generated documents from the project? That might be async though right?

        // PROTOTYPE: how do we handle getting the snapshot and not doing it multiple times?
        //            probably need the TCS pattern thingy again

        var snapshotService = State.Services.GetService<IGeneratorSnapshotProvider>();
        //Microsoft.CodeAnalysis.CodeAnalysisEventSource.Log.Message(this.State.Services.Workspace.CurrentSolution.Id.Id + HostProject.FilePath + ":Snaphost sevice is null: "+(snapshotService is null));
        
        if (snapshotService is not null)
        {
            var result = await snapshotService.GetGenerateDocumentsAsync(documentSnapshot);
            return result;
        }

        //if (_runResult is null)
        //{
        //    var project = State.Services.Workspace.CurrentSolution.Projects.SingleOrDefault(p => FilePathComparer.Instance.Equals(p.FilePath, HostProject.FilePath.Replace('/', '\\')));
        //    if (project is not null)
        //    {
        //        _runResult = await project.GetGeneratorRunResultAsync().ConfigureAwait(false);
        //        if (_runResult is not null)
        //        {
        //            Microsoft.CodeAnalysis.CodeAnalysisEventSource.Log.Message(this.State.Services.Workspace.CurrentSolution.Id.Id + HostProject.FilePath + ": Got Run result!");
        //        }
        //        else
        //        {
        //            Microsoft.CodeAnalysis.CodeAnalysisEventSource.Log.Message(this.State.Services.Workspace.CurrentSolution.Id.Id + HostProject.FilePath + ": Run result was null");
        //        }
        //    }
        //    else
        //    {
        //        Microsoft.CodeAnalysis.CodeAnalysisEventSource.Log.Message(this.State.Services.Workspace.CurrentSolution.Id.Id + HostProject.FilePath + ": Project was null");
        //    }
        //}

        // TODO: extract from the run-result the actual file

        // PROTOTYPE: how do we handle the case where we couldn't get the result?
        return ("", "", "");
    }
}
