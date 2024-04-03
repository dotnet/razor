// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal sealed class SnapshotResolver : ISnapshotResolver
{
    private readonly IProjectSnapshotManager _projectManager;
    private readonly ILogger _logger;
    private readonly Task _initializeTask;

    // Internal for testing
    internal readonly HostProject MiscellaneousHostProject;

    public SnapshotResolver(IProjectSnapshotManager projectManager, ILoggerFactory loggerFactory)
    {
        _projectManager = projectManager;
        _logger = loggerFactory.GetOrCreateLogger<SnapshotResolver>();

        var miscellaneousProjectPath = Path.Combine(TempDirectory.Instance.DirectoryPath, "__MISC_RAZOR_PROJECT__");
        var normalizedPath = FilePathNormalizer.Normalize(miscellaneousProjectPath);
        MiscellaneousHostProject = new HostProject(normalizedPath, normalizedPath, FallbackRazorConfiguration.Latest, rootNamespace: null, "Miscellaneous Files");

        _initializeTask = _projectManager.UpdateAsync(
            static (updater, miscFilesHostProject) => updater.ProjectAdded(miscFilesHostProject),
            state: MiscellaneousHostProject,
            CancellationToken.None);
    }

    /// <inheritdoc/>
    public IEnumerable<IProjectSnapshot> FindPotentialProjects(string documentFilePath)
    {
        if (documentFilePath is null)
        {
            throw new ArgumentNullException(nameof(documentFilePath));
        }

        var projects = _projectManager.GetProjects();
        var normalizedDocumentPath = FilePathNormalizer.Normalize(documentFilePath);
        foreach (var projectSnapshot in projects)
        {
            // Always exclude the miscellaneous project.
            if (projectSnapshot.FilePath == MiscellaneousHostProject.FilePath)
            {
                continue;
            }

            var projectDirectory = FilePathNormalizer.GetNormalizedDirectoryName(projectSnapshot.FilePath);
            if (normalizedDocumentPath.StartsWith(projectDirectory, FilePathComparison.Instance))
            {
                yield return projectSnapshot;
            }
        }
    }

    public IProjectSnapshot GetMiscellaneousProject()
    {
        if (!_projectManager.TryGetLoadedProject(MiscellaneousHostProject.Key, out var miscellaneousProject))
        {
            if (_initializeTask.Status is not (TaskStatus.RanToCompletion or TaskStatus.Faulted or TaskStatus.Canceled))
            {
                Debug.Fail("We should not have been asked for the miscellaneous project before it has been added.");

                // We don't expect this to happen, but if it does, we'll block and wait for the miscellaneous project
                // to be added before attempting to return it.

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                _initializeTask.Wait();
#pragma warning restore VSTHRD002
            }

            miscellaneousProject = _projectManager.GetLoadedProject(MiscellaneousHostProject.Key);
        }

        return miscellaneousProject;
    }

    public bool TryResolveDocumentInAnyProject(string documentFilePath, [NotNullWhen(true)] out IDocumentSnapshot? documentSnapshot)
    {
        _logger.LogTrace("Looking for {documentFilePath}.", documentFilePath);

        if (documentFilePath is null)
        {
            throw new ArgumentNullException(nameof(documentFilePath));
        }

        documentSnapshot = null;

        var normalizedDocumentPath = FilePathNormalizer.Normalize(documentFilePath);
        var potentialProjects = FindPotentialProjects(documentFilePath);
        foreach (var project in potentialProjects)
        {
            documentSnapshot = project.GetDocument(normalizedDocumentPath);
            if (documentSnapshot is not null)
            {
                _logger.LogTrace("Found {documentFilePath} in {project}", documentFilePath, project.FilePath);
                return true;
            }
        }

        _logger.LogTrace("Looking for {documentFilePath} in miscellaneous project.", documentFilePath);
        var miscellaneousProject = GetMiscellaneousProject();
        documentSnapshot = miscellaneousProject.GetDocument(normalizedDocumentPath);
        if (documentSnapshot is not null)
        {
            _logger.LogTrace("Found {documentFilePath} in miscellaneous project.", documentFilePath);
            return true;
        }

        _logger.LogTrace("{documentFilePath} not found in {documents}", documentFilePath, _projectManager.GetProjects().SelectMany(p => p.DocumentFilePaths));

        documentSnapshot = null;
        return false;
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal sealed class TestAccessor(SnapshotResolver instance)
    {
        public Task WaitForMiscFilesProjectAsync()
        {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
            return instance._initializeTask;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
        }
    }
}
