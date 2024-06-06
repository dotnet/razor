// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal static class IProjectSnapshotManagerExtensions
{
    public static IProjectSnapshot GetMiscellaneousProject(this IProjectSnapshotManager projectManager)
    {
        return projectManager.GetLoadedProject(MiscFilesHostProject.Instance.Key);
    }

    /// <summary>
    /// Finds all the projects where the document path starts with the path of the folder that contains the project file.
    /// </summary>
    public static ImmutableArray<IProjectSnapshot> FindPotentialProjects(this IProjectSnapshotManager projectManager, string documentFilePath)
    {
        var normalizedDocumentPath = FilePathNormalizer.Normalize(documentFilePath);

        using var projects = new PooledArrayBuilder<IProjectSnapshot>();

        foreach (var project in projectManager.GetProjects())
        {
            // Always exclude the miscellaneous project.
            if (project.FilePath == MiscFilesHostProject.Instance.FilePath)
            {
                continue;
            }

            var projectDirectory = FilePathNormalizer.GetNormalizedDirectoryName(project.FilePath);
            if (normalizedDocumentPath.StartsWith(projectDirectory, FilePathComparison.Instance))
            {
                projects.Add(project);
            }
        }

        return projects.DrainToImmutable();
    }

    public static bool TryResolveAllProjects(
        this IProjectSnapshotManager projectManager,
        string documentFilePath,
        out ImmutableArray<IProjectSnapshot> projects)
    {
        var potentialProjects = projectManager.FindPotentialProjects(documentFilePath);

        using var builder = new PooledArrayBuilder<IProjectSnapshot>(capacity: potentialProjects.Length);

        foreach (var project in potentialProjects)
        {
            if (project.GetDocument(documentFilePath) is not null)
            {
                builder.Add(project);
            }
        }

        var normalizedDocumentPath = FilePathNormalizer.Normalize(documentFilePath);
        var miscProject = projectManager.GetMiscellaneousProject();
        if (miscProject.GetDocument(normalizedDocumentPath) is not null)
        {
            builder.Add(miscProject);
        }

        projects = builder.DrainToImmutable();
        return projects.Length > 0;
    }
}
