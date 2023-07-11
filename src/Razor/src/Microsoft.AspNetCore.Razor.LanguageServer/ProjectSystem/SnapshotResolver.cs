// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
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
        MiscellaneousHostProject = new HostProject(FilePathNormalizer.Normalize(miscellaneousProjectPath), RazorDefaults.Configuration, RazorDefaults.RootNamespace);
    }

    /// <inheritdoc/>
    public IEnumerable<IProjectSnapshot> FindPotentialProjects(string documentFilePath, bool includeMiscellaneous)
    {
        if (documentFilePath is null)
        {
            throw new ArgumentNullException(nameof(documentFilePath));
        }

        var projects = _projectSnapshotManagerAccessor.Instance.Projects;
        var normalizedDocumentPath = FilePathNormalizer.Normalize(documentFilePath);
        foreach (var projectSnapshot in projects)
        {
            // Always include misc as something to check
            if (projectSnapshot.FilePath == MiscellaneousHostProject.FilePath)
            {
                if (includeMiscellaneous)
                {
                    yield return projectSnapshot;
                }

                continue;
            }

            var projectDirectory = FilePathNormalizer.GetDirectory(projectSnapshot.FilePath);
            if (normalizedDocumentPath.StartsWith(projectDirectory, FilePathComparison.Instance))
            {
                yield return projectSnapshot;
            }
        }
    }

    /// <inheritdoc />
    public bool TryResolve(string documentFilePath, bool includeMiscellaneous, [NotNullWhen(true)] out IProjectSnapshot? projectSnapshot, [NotNullWhen(true)] out IDocumentSnapshot? document)
    {
        _logger.LogTrace("Looking for {documentFilePath}. IncludeMiscellaneous: {includeMiscellaneous}", documentFilePath, includeMiscellaneous);

        if (documentFilePath is null)
        {
            throw new ArgumentNullException(nameof(documentFilePath));
        }

        document = null;

        var normalizedDocumentPath = FilePathNormalizer.Normalize(documentFilePath);
        var potentialProjects = FindPotentialProjects(documentFilePath, includeMiscellaneous);
        foreach (var project in potentialProjects)
        {
            document = project.GetDocument(normalizedDocumentPath);
            if (document is not null)
            {
                _logger.LogTrace("Found {documentFilePath} in {project}", documentFilePath, project.FilePath);
                projectSnapshot = project;
                return true;
            }
        }

        _logger.LogTrace("{documentFilePath} not found in {documents}", documentFilePath, _projectSnapshotManagerAccessor.Instance.Projects.SelectMany(p => p.DocumentFilePaths));

        document = null;
        projectSnapshot = null;
        return false;
    }

    public IProjectSnapshot GetMiscellaneousProject()
    {
        var miscellaneousProject = _projectSnapshotManagerAccessor.Instance.GetLoadedProject(MiscellaneousHostProject.FilePath);
        if (miscellaneousProject is null)
        {
            _projectSnapshotManagerAccessor.Instance.ProjectAdded(MiscellaneousHostProject);
            miscellaneousProject = _projectSnapshotManagerAccessor.Instance.GetLoadedProject(MiscellaneousHostProject.FilePath);
        }

        return miscellaneousProject;
    }

    public bool TryResolveDocument(string documentFilePath, bool includeMiscellaneous, [NotNullWhen(true)] out IDocumentSnapshot? documentSnapshot)
        => TryResolve(documentFilePath, includeMiscellaneous, out var _, out documentSnapshot);

    public bool TryResolveProject(string documentFilePath, bool includeMiscellaneous, [NotNullWhen(true)] out IProjectSnapshot? projectSnapshot)
        => TryResolve(documentFilePath, includeMiscellaneous, out projectSnapshot, out var _);
}
