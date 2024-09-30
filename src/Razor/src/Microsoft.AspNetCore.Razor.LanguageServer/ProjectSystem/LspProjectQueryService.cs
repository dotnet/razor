// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal sealed class LspProjectQueryService(IProjectSnapshotManager projectSnapshotManager) : IProjectQueryService
{
    private readonly IProjectSnapshotManager _projectSnapshotManager = projectSnapshotManager;

    public ImmutableArray<IProjectSnapshot> GetProjects()
    {
        return _projectSnapshotManager.GetProjects();
    }

    public ImmutableArray<IProjectSnapshot> FindProjects(string documentFilePath)
    {
        using var results = new PooledArrayBuilder<IProjectSnapshot>();

        foreach (var project in _projectSnapshotManager.GetProjects())
        {
            if (!project.TryGetDocument(documentFilePath, out _))
            {
                results.Add(project);
            }
        }

        return results.DrainToImmutable();
    }
}
