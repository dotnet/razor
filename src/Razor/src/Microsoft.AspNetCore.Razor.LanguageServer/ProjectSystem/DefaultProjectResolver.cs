// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal class DefaultProjectResolver : ProjectResolver
{
    // Internal for testing
    protected internal readonly HostProject MiscellaneousHostProject;

    private readonly ProjectSnapshotManagerAccessor _projectSnapshotManagerAccessor;

    public DefaultProjectResolver(
        ProjectSnapshotManagerAccessor projectSnapshotManagerAccessor)
    {
        _projectSnapshotManagerAccessor = projectSnapshotManagerAccessor ?? throw new ArgumentNullException(nameof(projectSnapshotManagerAccessor));

        var miscellaneousProjectPath = Path.Combine(TempDirectory.Instance.DirectoryPath, "__MISC_RAZOR_PROJECT__");
        MiscellaneousHostProject = new HostProject(miscellaneousProjectPath, RazorDefaults.Configuration, RazorDefaults.RootNamespace);
    }

    public override bool TryResolveProject(string documentFilePath, [NotNullWhen(true)] out IProjectSnapshot? projectSnapshot)
        => TryResolve(documentFilePath, out projectSnapshot, out var _);

    public override bool TryResolve(string documentFilePath, [NotNullWhen(true)] out IProjectSnapshot? projectSnapshot, [NotNullWhen(true)] out IDocumentSnapshot? document)
    {
        if (documentFilePath is null)
        {
            throw new ArgumentNullException(nameof(documentFilePath));
        }

        document = null;

        var normalizedDocumentPath = FilePathNormalizer.Normalize(documentFilePath);
        var projects = _projectSnapshotManagerAccessor.Instance.Projects;
        for (var i = 0; i < projects.Length; i++)
        {
            projectSnapshot = projects[i];

            if (projectSnapshot.FilePath == MiscellaneousHostProject.FilePath)
            {
                document = projectSnapshot.GetDocument(documentFilePath);
                if (document is not null)
                {
                    return true;
                }

                continue;
            }

            var projectDirectory = FilePathNormalizer.GetDirectory(projectSnapshot.FilePath);
            if (normalizedDocumentPath.StartsWith(projectDirectory, FilePathComparison.Instance))
            {
                continue;
            }

            document = projectSnapshot.GetDocument(documentFilePath);
            if (document is not null)
            {
                return true;
            }
        }

        projectSnapshot = null;
        return false;
    }

    public override IProjectSnapshot GetMiscellaneousProject()
        => _projectSnapshotManagerAccessor.Instance.GetOrAddLoadedProject(
            MiscellaneousHostProject.FilePath,
            MiscellaneousHostProject.Configuration,
            MiscellaneousHostProject.RootNamespace);
}
