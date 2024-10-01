// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

internal sealed class RemoteSolutionSnapshot(Solution solution, RemoteSnapshotManager snapshotManager) : IProjectQueryService
{
    public RemoteSnapshotManager SnapshotManager { get; } = snapshotManager;

    private readonly Solution _solution = solution;
    private readonly Dictionary<Project, RemoteProjectSnapshot> _projectMap = [];

    public RemoteProjectSnapshot GetProject(ProjectId projectId)
    {
        var project = _solution.GetRequiredProject(projectId);
        return GetProject(project);
    }

    public RemoteProjectSnapshot GetProject(Project project)
    {
        if (project.Solution != _solution)
        {
            throw new InvalidOperationException();
        }

        lock (_projectMap)
        {
            if (!_projectMap.TryGetValue(project, out var snapshot))
            {
                snapshot = new RemoteProjectSnapshot(project, this);
                _projectMap.Add(project, snapshot);
            }

            return snapshot;
        }
    }

    private ImmutableArray<IProjectSnapshot> _projects;

    public ImmutableArray<IProjectSnapshot> GetProjects()
    {
        if (_projects.IsDefault)
        {
            ImmutableInterlocked.InterlockedInitialize(ref _projects, ComputeProjects());
        }

        return _projects;

        ImmutableArray<IProjectSnapshot> ComputeProjects()
        {
            var projectIds = _solution.ProjectIds;

            if (projectIds.Count == 0)
            {
                return [];
            }

            using var results = new PooledArrayBuilder<IProjectSnapshot>(capacity: projectIds.Count);

            foreach (var projectId in projectIds)
            {
                var project = GetProject(projectId);
                results.Add(project);
            }

            return results.DrainToImmutable();

        }
    }

    public ImmutableArray<IProjectSnapshot> FindProjects(string documentFilePath)
    {
        var documentIds = _solution.GetDocumentIdsWithFilePath(documentFilePath);

        if (documentIds.IsEmpty)
        {
            return [];
        }

        using var results = new PooledArrayBuilder<IProjectSnapshot>(capacity: documentIds.Length);
        using var _ = HashSetPool<ProjectId>.GetPooledObject(out var projectIdSet);

        foreach (var documentId in documentIds)
        {
            var projectId = documentId.ProjectId;

            // We use a set to ensure that we only ever return the same project once.
            if (projectIdSet.Add(projectId))
            {
                var project = GetProject(projectId);
                results.Add(project);
            }
        }

        return results.DrainToImmutable();
    }
}
