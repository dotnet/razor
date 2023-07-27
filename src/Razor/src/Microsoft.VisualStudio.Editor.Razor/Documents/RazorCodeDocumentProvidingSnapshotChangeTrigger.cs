﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.Editor.Razor.Documents;

[Export(typeof(RazorCodeDocumentProvidingSnapshotChangeTrigger))]
[Export(typeof(IProjectSnapshotChangeTrigger))]
[System.Composition.Shared]
internal class RazorCodeDocumentProvidingSnapshotChangeTrigger : IProjectSnapshotChangeTrigger
{
    private readonly HashSet<string> _openDocuments = new(FilePathComparer.Instance);
    private readonly Dictionary<string, ProjectKey> _documentProjectMap = new(FilePathComparer.Instance);
    private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
    private ProjectSnapshotManagerBase? _projectManager;

    public event EventHandler<string>? DocumentReady;

    [ImportingConstructor]
    public RazorCodeDocumentProvidingSnapshotChangeTrigger(ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher)
    {
        _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
    }

    public void Initialize(ProjectSnapshotManagerBase projectManager)
    {
        _projectManager = projectManager;

        _projectManager.Changed += ProjectManager_Changed;
    }

    private void ProjectManager_Changed(object sender, ProjectChangeEventArgs e)
    {
        if (e.Kind == ProjectChangeKind.ProjectAdded)
        {
            var projectDocuments = e.Newer!.DocumentFilePaths.ToArray();
            foreach (var doc in _openDocuments)
            {
                if (projectDocuments.Contains(doc))
                {
                    _openDocuments.Remove(doc);
                    DocumentReady?.Invoke(this, doc);
                }
            }
        }
        else if (e.Kind == ProjectChangeKind.DocumentAdded)
        {
            var documentFilePath = e.DocumentFilePath!;
            _documentProjectMap[documentFilePath] = e.ProjectKey;
            if (_openDocuments.Contains(documentFilePath))
            {
                _openDocuments.Remove(documentFilePath);
                DocumentReady?.Invoke(this, documentFilePath);
            }
        }
        else if (e.Kind == ProjectChangeKind.DocumentRemoved)
        {
            _documentProjectMap.Remove(e.DocumentFilePath!);
        }
    }

    public async Task<RazorCodeDocument?> GetRazorCodeDocumentAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!_documentProjectMap.TryGetValue(filePath, out var projectKey))
        {
            _openDocuments.Add(filePath);
            return null;
        }

        var project = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
            () => _projectManager?.GetLoadedProject(projectKey), cancellationToken);

        if (project is null)
        {
            return null;
        }

        var document = project.GetDocument(filePath);
        if (document is null)
        {
            return null;
        }

        var razorDocument = await document.GetGeneratedOutputAsync().ConfigureAwait(false);

        return razorDocument;
    }
}
