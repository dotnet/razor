﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal class DefaultRazorProjectService : RazorProjectService
{
    private readonly ProjectSnapshotManagerAccessor _projectSnapshotManagerAccessor;
    private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
    private readonly RemoteTextLoaderFactory _remoteTextLoaderFactory;
    private readonly ISnapshotResolver _snapshotResolver;
    private readonly DocumentVersionCache _documentVersionCache;
    private readonly ILogger _logger;

    public DefaultRazorProjectService(
        ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
        RemoteTextLoaderFactory remoteTextLoaderFactory,
        ISnapshotResolver snapshotResolver,
        DocumentVersionCache documentVersionCache,
        ProjectSnapshotManagerAccessor projectSnapshotManagerAccessor,
        ILoggerFactory loggerFactory)
    {
        if (projectSnapshotManagerDispatcher is null)
        {
            throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
        }

        if (remoteTextLoaderFactory is null)
        {
            throw new ArgumentNullException(nameof(remoteTextLoaderFactory));
        }

        if (snapshotResolver is null)
        {
            throw new ArgumentNullException(nameof(snapshotResolver));
        }

        if (documentVersionCache is null)
        {
            throw new ArgumentNullException(nameof(documentVersionCache));
        }

        if (projectSnapshotManagerAccessor is null)
        {
            throw new ArgumentNullException(nameof(projectSnapshotManagerAccessor));
        }

        if (loggerFactory is null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
        _remoteTextLoaderFactory = remoteTextLoaderFactory;
        _snapshotResolver = snapshotResolver;
        _documentVersionCache = documentVersionCache;
        _projectSnapshotManagerAccessor = projectSnapshotManagerAccessor;
        _logger = loggerFactory.CreateLogger<DefaultRazorProjectService>();
    }

    public override void AddDocument(string filePath)
    {
        _projectSnapshotManagerDispatcher.AssertDispatcherThread();

        var textDocumentPath = FilePathNormalizer.Normalize(filePath);

        var added = false;
        foreach (var projectSnapshot in _snapshotResolver.FindPotentialProjects(textDocumentPath))
        {
            added = true;
            AddDocumentToProject(projectSnapshot, textDocumentPath);
        }

        if (!added)
        {
            AddDocumentToProject(_snapshotResolver.GetMiscellaneousProject(), textDocumentPath);
        }

        void AddDocumentToProject(IProjectSnapshot projectSnapshot, string textDocumentPath)
        {
            if (_snapshotResolver.TryResolveDocument(projectSnapshot.Key, textDocumentPath, out var _))
            {
                // Document already added. This usually occurs when VSCode has already pre-initialized
                // open documents and then we try to manually add all known razor documents.
                return;
            }

            var targetFilePath = textDocumentPath;
            var projectDirectory = FilePathNormalizer.GetDirectory(projectSnapshot.FilePath);
            if (targetFilePath.StartsWith(projectDirectory, FilePathComparison.Instance))
            {
                // Make relative
                targetFilePath = textDocumentPath[projectDirectory.Length..];
            }

            // Representing all of our host documents with a re-normalized target path to workaround GetRelatedDocument limitations.
            var normalizedTargetFilePath = targetFilePath.Replace('/', '\\').TrimStart('\\');

            var hostDocument = new HostDocument(textDocumentPath, normalizedTargetFilePath);
            var textLoader = _remoteTextLoaderFactory.Create(textDocumentPath);

            _logger.LogInformation("Adding document '{filePath}' to project '{projectSnapshotFilePath}'.", filePath, projectSnapshot.FilePath);
            _projectSnapshotManagerAccessor.Instance.DocumentAdded(projectSnapshot.Key, hostDocument, textLoader);
        }
    }

    public override void OpenDocument(string filePath, SourceText sourceText, int version)
    {
        _projectSnapshotManagerDispatcher.AssertDispatcherThread();

        var textDocumentPath = FilePathNormalizer.Normalize(filePath);

        // We are okay to use the non-project-key overload of TryResolveDocument here because we really are just checking if the document
        // has been added to _any_ project. AddDocument will take care of adding to all of the necessary ones, and then below we ensure
        // we process them all too
        if (!_snapshotResolver.TryResolveDocumentInAnyProject(textDocumentPath, out _))
        {
            // Document hasn't been added. This usually occurs when VSCode trumps all other initialization
            // processes and pre-initializes already open documents.
            AddDocument(filePath);
        }

        ActOnDocumentInMultipleProjects(filePath, (projectSnapshot, textDocumentPath) =>
        {
            _logger.LogInformation("Opening document '{textDocumentPath}' in project '{projectSnapshotFilePath}'.", textDocumentPath, projectSnapshot.FilePath);
            _projectSnapshotManagerAccessor.Instance.DocumentOpened(projectSnapshot.Key, textDocumentPath, sourceText);

            TrackDocumentVersion(textDocumentPath, version);

            if (projectSnapshot.GetDocument(textDocumentPath) is { } documentSnapshot)
            {
                // Start generating the C# for the document so it can immediately be ready for incoming requests.
                _ = documentSnapshot.GetGeneratedOutputAsync();
            }
        });
    }

    public override void CloseDocument(string filePath)
    {
        _projectSnapshotManagerDispatcher.AssertDispatcherThread();

        ActOnDocumentInMultipleProjects(filePath, (projectSnapshot, textDocumentPath) =>
        {
            var textLoader = _remoteTextLoaderFactory.Create(filePath);
            _logger.LogInformation("Closing document '{textDocumentPath}' in project '{projectSnapshotFilePath}'.", textDocumentPath, projectSnapshot.FilePath);
            _projectSnapshotManagerAccessor.Instance.DocumentClosed(projectSnapshot.Key, textDocumentPath, textLoader);
        });
    }

    public override void RemoveDocument(string filePath)
    {
        _projectSnapshotManagerDispatcher.AssertDispatcherThread();

        ActOnDocumentInMultipleProjects(filePath, (projectSnapshot, textDocumentPath) =>
        {
            if (!projectSnapshot.DocumentFilePaths.Contains(textDocumentPath, FilePathComparer.Instance))
            {
                _logger.LogInformation("Containing project is not tracking document '{filePath}'", textDocumentPath);
                return;
            }

            if (projectSnapshot.GetDocument(textDocumentPath) is not DocumentSnapshot documentSnapshot)
            {
                _logger.LogError("Containing project does not contain document '{filePath}'", textDocumentPath);
                return;
            }

            // If the document is open, we can't remove it, because we could still get a request for it, and that
            // request would fail. Instead we move it to the miscellaneous project, just like if we got notified of
            // a remove via the project.razor.json
            if (_projectSnapshotManagerAccessor.Instance.IsDocumentOpen(textDocumentPath))
            {
                _logger.LogInformation("Moving document '{textDocumentPath}' from project '{projectSnapshotFilePath}' to misc files because it is open.", textDocumentPath, projectSnapshot.FilePath);
                var miscellaneousProject = (ProjectSnapshot)_snapshotResolver.GetMiscellaneousProject();
                if (projectSnapshot != miscellaneousProject)
                {
                    MoveDocument(textDocumentPath, projectSnapshot, miscellaneousProject);
                }
            }
            else
            {
                _logger.LogInformation("Removing document '{textDocumentPath}' from project '{projectSnapshotFilePath}'.", textDocumentPath, projectSnapshot.FilePath);
                _projectSnapshotManagerAccessor.Instance.DocumentRemoved(projectSnapshot.Key, documentSnapshot.State.HostDocument);
            }
        });
    }

    public override void UpdateDocument(string filePath, SourceText sourceText, int version)
    {
        _projectSnapshotManagerDispatcher.AssertDispatcherThread();

        ActOnDocumentInMultipleProjects(filePath, (project, textDocumentPath) =>
        {
            _logger.LogTrace("Updating document '{textDocumentPath}' in {projectKey}.", textDocumentPath, project.Key);
            _projectSnapshotManagerAccessor.Instance.DocumentChanged(project.Key, textDocumentPath, sourceText);

            TrackDocumentVersion(textDocumentPath, version);
        });
    }

    private void ActOnDocumentInMultipleProjects(string filePath, Action<IProjectSnapshot, string> action)
    {
        var textDocumentPath = FilePathNormalizer.Normalize(filePath);
        if (!_snapshotResolver.TryResolveAllProjects(textDocumentPath, out var projectSnapshots))
        {
            projectSnapshots = new[] { _snapshotResolver.GetMiscellaneousProject() };
        }

        foreach (var project in projectSnapshots)
        {
            action(project, textDocumentPath);
        }
    }

    public override ProjectKey AddProject(string filePath, string intermediateOutputPath, string? rootNamespace)
    {
        _projectSnapshotManagerDispatcher.AssertDispatcherThread();

        var normalizedPath = FilePathNormalizer.Normalize(filePath);
        var hostProject = new HostProject(normalizedPath, intermediateOutputPath, RazorDefaults.Configuration, rootNamespace ?? RazorDefaults.RootNamespace);
        // ProjectAdded will no-op if the project already exists
        _projectSnapshotManagerAccessor.Instance.ProjectAdded(hostProject);

        _logger.LogInformation("Added project '{filePath}' to project system.", filePath);

        TryMigrateMiscellaneousDocumentsToProject();

        return hostProject.Key;
    }

    public override void RemoveProject(string filePath)
    {
        _projectSnapshotManagerDispatcher.AssertDispatcherThread();

        var normalizedPath = FilePathNormalizer.Normalize(filePath);

        var projectKeys = _projectSnapshotManagerAccessor.Instance.GetAllProjectKeys(normalizedPath);
        foreach (var projectKey in projectKeys)
        {
            var project = (ProjectSnapshot)_projectSnapshotManagerAccessor.Instance.GetLoadedProject(projectKey);

            if (project is null)
            {
                // Never tracked the project to begin with, noop.
                continue;
            }

            _logger.LogInformation("Removing project '{filePath}' from project system.", filePath);
            _projectSnapshotManagerAccessor.Instance.ProjectRemoved(project.Key);

            TryMigrateDocumentsFromRemovedProject(project);
        }
    }

    public override void UpdateProject(
        ProjectKey projectKey,
        RazorConfiguration? configuration,
        string? rootNamespace,
        ProjectWorkspaceState projectWorkspaceState,
        IReadOnlyList<DocumentSnapshotHandle> documents)
    {
        _projectSnapshotManagerDispatcher.AssertDispatcherThread();

        var project = (ProjectSnapshot)_projectSnapshotManagerAccessor.Instance.GetLoadedProject(projectKey);

        if (project is null)
        {
            // Never tracked the project to begin with, noop.
            _logger.LogInformation("Failed to update untracked project '{projectKey}'.", projectKey);
            return;
        }

        UpdateProjectDocuments(documents, project.Key);

        if (!projectWorkspaceState.Equals(ProjectWorkspaceState.Default))
        {
            _logger.LogInformation("Updating project '{filePath}' TagHelpers ({projectWorkspaceState.TagHelpers.Count}) and C# Language Version ({projectWorkspaceState.CSharpLanguageVersion}).",
                project.FilePath, projectWorkspaceState.TagHelpers.Length, projectWorkspaceState.CSharpLanguageVersion);
        }

        _projectSnapshotManagerAccessor.Instance.ProjectWorkspaceStateChanged(project.Key, projectWorkspaceState);

        var currentHostProject = project.HostProject;
        var currentConfiguration = currentHostProject.Configuration;
        if (currentConfiguration.ConfigurationName == configuration?.ConfigurationName &&
            currentHostProject.RootNamespace == rootNamespace)
        {
            _logger.LogTrace("Updating project '{filePath}'. The project is already using configuration '{configuration.ConfigurationName}' and root namespace '{rootNamespace}'.",
                project.FilePath, configuration.ConfigurationName, rootNamespace);
            return;
        }

        if (configuration is null)
        {
            configuration = RazorDefaults.Configuration;
            _logger.LogInformation("Updating project '{filePath}' to use Razor's default configuration ('{configuration.ConfigurationName}')'.", project.FilePath, configuration.ConfigurationName);
        }
        else if (currentConfiguration.ConfigurationName != configuration.ConfigurationName)
        {
            _logger.LogInformation("Updating project '{filePath}' to Razor configuration '{configuration.ConfigurationName}' with language version '{configuration.LanguageVersion}'.",
                project.FilePath, configuration.ConfigurationName, configuration.LanguageVersion);
        }

        if (currentHostProject.RootNamespace != rootNamespace)
        {
            _logger.LogInformation("Updating project '{filePath}''s root namespace to '{rootNamespace}'.", project.FilePath, rootNamespace);
        }

        var hostProject = new HostProject(project.FilePath, project.IntermediateOutputPath, configuration, rootNamespace);
        _projectSnapshotManagerAccessor.Instance.ProjectConfigurationChanged(hostProject);
    }

    private void UpdateProjectDocuments(IReadOnlyList<DocumentSnapshotHandle> documents, ProjectKey projectKey)
    {
        var project = (ProjectSnapshot)_projectSnapshotManagerAccessor.Instance.GetLoadedProject(projectKey);
        var currentHostProject = project.HostProject;
        var projectDirectory = FilePathNormalizer.GetDirectory(project.FilePath);
        var documentMap = documents.ToDictionary(document => EnsureFullPath(document.FilePath, projectDirectory), FilePathComparer.Instance);
        var miscellaneousProject = (ProjectSnapshot)_snapshotResolver.GetMiscellaneousProject();

        // "Remove" any unnecessary documents by putting them into the misc project
        foreach (var documentFilePath in project.DocumentFilePaths)
        {
            if (documentMap.ContainsKey(documentFilePath))
            {
                // This document still exists in the updated project
                continue;
            }

            MoveDocument(documentFilePath, project, miscellaneousProject);
        }

        project = (ProjectSnapshot)_projectSnapshotManagerAccessor.Instance.GetLoadedProject(projectKey);

        // Update existing documents
        foreach (var documentFilePath in project.DocumentFilePaths)
        {
            if (!documentMap.TryGetValue(documentFilePath, out var documentHandle))
            {
                // Document exists in the project but not in the configured documents. Chances are the project configuration is from a fallback
                // configuration case (< 2.1) or the project isn't fully loaded yet.
                continue;
            }

            if (project.GetDocument(documentFilePath) is not DocumentSnapshot documentSnapshot)
            {
                continue;
            }

            var currentHostDocument = documentSnapshot.State.HostDocument;
            var newFilePath = EnsureFullPath(documentHandle.FilePath, projectDirectory);
            var newHostDocument = new HostDocument(newFilePath, documentHandle.TargetPath, documentHandle.FileKind);

            if (HostDocumentComparer.Instance.Equals(currentHostDocument, newHostDocument))
            {
                // Current and "new" host documents are equivalent
                continue;
            }

            _logger.LogTrace("Updating document '{newHostDocument.FilePath}''s file kind to '{newHostDocument.FileKind}' and target path to '{newHostDocument.TargetPath}'.",
                newHostDocument.FilePath, newHostDocument.FileKind, newHostDocument.TargetPath);

            _projectSnapshotManagerAccessor.Instance.DocumentRemoved(currentHostProject.Key, currentHostDocument);

            var remoteTextLoader = _remoteTextLoaderFactory.Create(newFilePath);
            _projectSnapshotManagerAccessor.Instance.DocumentAdded(currentHostProject.Key, newHostDocument, remoteTextLoader);
        }

        project = (ProjectSnapshot)_projectSnapshotManagerAccessor.Instance.GetLoadedProject(project.Key);
        miscellaneousProject = (ProjectSnapshot)_snapshotResolver.GetMiscellaneousProject();

        // Add (or migrate from misc) any new documents
        foreach (var documentKvp in documentMap)
        {
            var documentFilePath = documentKvp.Key;
            if (project.DocumentFilePaths.Contains(documentFilePath, FilePathComparer.Instance))
            {
                // Already know about this document
                continue;
            }

            if (miscellaneousProject.DocumentFilePaths.Contains(documentFilePath, FilePathComparer.Instance))
            {
                MoveDocument(documentFilePath, miscellaneousProject, project);
            }
            else
            {
                var documentHandle = documentKvp.Value;
                var remoteTextLoader = _remoteTextLoaderFactory.Create(documentFilePath);
                var newHostDocument = new HostDocument(documentFilePath, documentHandle.TargetPath, documentHandle.FileKind);

                _logger.LogInformation("Adding new document '{documentFilePath}' to project '{projectFilePath}'.", documentFilePath, currentHostProject.FilePath);
                _projectSnapshotManagerAccessor.Instance.DocumentAdded(currentHostProject.Key, newHostDocument, remoteTextLoader);
            }
        }
    }

    private void MoveDocument(string documentFilePath, IProjectSnapshot fromProject, ProjectSnapshot toProject)
    {
        Debug.Assert(fromProject.DocumentFilePaths.Contains(documentFilePath, FilePathComparer.Instance));
        Debug.Assert(!toProject.DocumentFilePaths.Contains(documentFilePath, FilePathComparer.Instance));

        if (fromProject.GetDocument(documentFilePath) is not DocumentSnapshot documentSnapshot)
        {
            return;
        }

        var currentHostDocument = documentSnapshot.State.HostDocument;

        var textLoader = new DocumentSnapshotTextLoader(documentSnapshot);
        var newHostDocument = new HostDocument(documentSnapshot.FilePath, documentSnapshot.TargetPath, documentSnapshot.FileKind);

        _logger.LogInformation("Moving '{documentFilePath}' from the '{fromProject.FilePath}' project to '{toProject.FilePath}' project.",
            documentFilePath, fromProject.FilePath, toProject.FilePath);
        _projectSnapshotManagerAccessor.Instance.DocumentRemoved(fromProject.Key, currentHostDocument);
        _projectSnapshotManagerAccessor.Instance.DocumentAdded(toProject.Key, newHostDocument, textLoader);
    }

    private static string EnsureFullPath(string filePath, string projectDirectory)
    {
        var normalizedFilePath = FilePathNormalizer.Normalize(filePath);
        if (!normalizedFilePath.StartsWith(projectDirectory, FilePathComparison.Instance))
        {
            var absolutePath = Path.Combine(projectDirectory, normalizedFilePath);
            normalizedFilePath = FilePathNormalizer.Normalize(absolutePath);
        }

        return normalizedFilePath;
    }

    // Internal for testing
    internal void TryMigrateDocumentsFromRemovedProject(IProjectSnapshot project)
    {
        _projectSnapshotManagerDispatcher.AssertDispatcherThread();

        var miscellaneousProject = _snapshotResolver.GetMiscellaneousProject();

        foreach (var documentFilePath in project.DocumentFilePaths)
        {
            if (project.GetDocument(documentFilePath) is not DocumentSnapshot documentSnapshot)
            {
                continue;
            }

            var toProject = _snapshotResolver.FindPotentialProjects(documentFilePath).FirstOrDefault()
                ?? miscellaneousProject;

            var textLoader = new DocumentSnapshotTextLoader(documentSnapshot);
            var defaultToProject = (ProjectSnapshot)toProject;
            var newHostDocument = new HostDocument(documentSnapshot.FilePath, documentSnapshot.TargetPath, documentSnapshot.FileKind);

            _logger.LogInformation("Migrating '{documentFilePath}' from the '{project.FilePath}' project to '{toProject.FilePath}' project.",
                documentFilePath, project.FilePath, toProject.FilePath);
            _projectSnapshotManagerAccessor.Instance.DocumentAdded(defaultToProject.Key, newHostDocument, textLoader);
        }
    }

    // Internal for testing
    internal void TryMigrateMiscellaneousDocumentsToProject()
    {
        _projectSnapshotManagerDispatcher.AssertDispatcherThread();

        var miscellaneousProject = _snapshotResolver.GetMiscellaneousProject();

        foreach (var documentFilePath in miscellaneousProject.DocumentFilePaths)
        {
            var projectSnapshot = _snapshotResolver.FindPotentialProjects(documentFilePath).FirstOrDefault();
            if (projectSnapshot is null)
            {
                continue;
            }

            if (miscellaneousProject.GetDocument(documentFilePath) is not DocumentSnapshot documentSnapshot)
            {
                continue;
            }

            // Remove from miscellaneous project
            var defaultMiscProject = (ProjectSnapshot)miscellaneousProject;
            _projectSnapshotManagerAccessor.Instance.DocumentRemoved(defaultMiscProject.Key, documentSnapshot.State.HostDocument);

            // Add to new project

            var textLoader = new DocumentSnapshotTextLoader(documentSnapshot);
            var defaultProject = (ProjectSnapshot)projectSnapshot;
            var newHostDocument = new HostDocument(documentSnapshot.FilePath, documentSnapshot.TargetPath);
            _logger.LogInformation("Migrating '{documentFilePath}' from the '{miscellaneousProject.FilePath}' project to '{projectSnapshot.FilePath}' project.",
                documentFilePath, miscellaneousProject.FilePath, projectSnapshot.FilePath);
            _projectSnapshotManagerAccessor.Instance.DocumentAdded(defaultProject.Key, newHostDocument, textLoader);
        }
    }

    private void TrackDocumentVersion(string textDocumentPath, int version)
    {
        // TODO: This should take in the document snapshot
        if (!_snapshotResolver.TryResolveDocument(textDocumentPath, out var documentSnapshot))
        {
            return;
        }

        _documentVersionCache.TrackDocumentVersion(documentSnapshot, version);
    }

    private class DelegatingTextLoader : TextLoader
    {
        private readonly IDocumentSnapshot _fromDocument;
        public DelegatingTextLoader(IDocumentSnapshot fromDocument)
        {
            _fromDocument = fromDocument ?? throw new ArgumentNullException(nameof(fromDocument));
        }
        public override async Task<TextAndVersion> LoadTextAndVersionAsync(
           LoadTextOptions options,
           CancellationToken cancellationToken)
        {
            var sourceText = await _fromDocument.GetTextAsync().ConfigureAwait(false);
            var version = await _fromDocument.GetTextVersionAsync().ConfigureAwait(false);
            var textAndVersion = TextAndVersion.Create(sourceText, version.GetNewerVersion());
            return textAndVersion;
        }
    }
}
