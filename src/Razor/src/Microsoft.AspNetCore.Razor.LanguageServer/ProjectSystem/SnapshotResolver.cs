﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal class SnapshotResolver : ISnapshotResolver
{
    private readonly ProjectSnapshotManagerAccessor _projectSnapshotManagerAccessor;
    private readonly ILogger<SnapshotResolver> _logger;

    // Internal for testing
    internal readonly HostProject MiscellaneousHostProject;

    public SnapshotResolver(ProjectSnapshotManagerAccessor projectSnapshotManagerAccessor, ILoggerFactory loggerFactory)
    {
        _projectSnapshotManagerAccessor = projectSnapshotManagerAccessor ?? throw new ArgumentNullException(nameof(projectSnapshotManagerAccessor));
        _logger = loggerFactory?.CreateLogger<SnapshotResolver>() ?? throw new ArgumentNullException(nameof(loggerFactory));

        var miscellaneousProjectPath = Path.Combine(TempDirectory.Instance.DirectoryPath, "__MISC_RAZOR_PROJECT__");
        var normalizedPath = FilePathNormalizer.Normalize(miscellaneousProjectPath);
        MiscellaneousHostProject = new HostProject(normalizedPath, normalizedPath, FallbackRazorConfiguration.Latest, rootNamespace: null);
    }

    /// <inheritdoc/>
    public IEnumerable<IProjectSnapshot> FindPotentialProjects(string documentFilePath)
    {
        if (documentFilePath is null)
        {
            throw new ArgumentNullException(nameof(documentFilePath));
        }

        var projects = _projectSnapshotManagerAccessor.Instance.GetProjects();
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
        var miscellaneousProject = _projectSnapshotManagerAccessor.Instance.GetLoadedProject(MiscellaneousHostProject.Key);
        if (miscellaneousProject is null)
        {
            _projectSnapshotManagerAccessor.Instance.ProjectAdded(MiscellaneousHostProject);
            miscellaneousProject = _projectSnapshotManagerAccessor.Instance.GetLoadedProject(MiscellaneousHostProject.Key);
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

        _logger.LogTrace("{documentFilePath} not found in {documents}", documentFilePath, _projectSnapshotManagerAccessor.Instance.GetProjects().SelectMany(p => p.DocumentFilePaths));

        documentSnapshot = null;
        return false;
    }
}
