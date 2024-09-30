// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

[Export(typeof(ProjectQueryServiceFactory)), Shared]
[method: ImportingConstructor]
internal sealed class ProjectQueryServiceFactory(ProjectSnapshotFactory projectSnapshotFactory)
{
    public IProjectQueryService Create(Solution solution)
        => new Service(solution, projectSnapshotFactory);

    private sealed class Service(Solution solution, ProjectSnapshotFactory projectSnapshotFactory) : IProjectQueryService
    {
        private readonly Solution _solution = solution;
        private readonly ProjectSnapshotFactory _projectSnapshotFactory = projectSnapshotFactory;

        private ImmutableArray<IProjectSnapshot> _projects;

        public ImmutableArray<IProjectSnapshot> GetProjects()
        {
            if (_projects.IsDefault)
            {
                ImmutableInterlocked.InterlockedInitialize(ref _projects, ComputeProjects(_solution, _projectSnapshotFactory));
            }

            return _projects;

            static ImmutableArray<IProjectSnapshot> ComputeProjects(Solution solution, ProjectSnapshotFactory factory)
            {
                var projectIds = solution.ProjectIds;

                if (projectIds.Count == 0)
                {
                    return [];
                }

                using var results = new PooledArrayBuilder<IProjectSnapshot>(capacity: projectIds.Count);

                foreach (var projectId in projectIds)
                {
                    if (solution.GetProject(projectId) is Project project)
                    {
                        results.Add(factory.GetOrCreate(project));
                    }
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
                // We use a set to ensure that we only ever return the same project once.
                if (!projectIdSet.Add(documentId.ProjectId))
                {
                    continue;
                }

                if (_solution.GetProject(documentId.ProjectId) is Project project)
                {
                    results.Add(_projectSnapshotFactory.GetOrCreate(project));
                }
            }

            return results.DrainToImmutable();
        }
    }
}
