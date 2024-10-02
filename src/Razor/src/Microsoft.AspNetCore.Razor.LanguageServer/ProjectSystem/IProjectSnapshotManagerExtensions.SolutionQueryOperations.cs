// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal static partial class IProjectSnapshotManagerExtensions
{
    private sealed class SolutionQueryOperations(IProjectSnapshotManager projectManager) : ISolutionQueryOperations
    {
        private readonly IProjectSnapshotManager _projectManager = projectManager;

        public IEnumerable<IProjectSnapshot> GetProjects()
        {
            return _projectManager.GetProjects();
        }

        public ImmutableArray<IProjectSnapshot> FindProjects(string documentFilePath)
        {
            using var results = new PooledArrayBuilder<IProjectSnapshot>();

            foreach (var project in _projectManager.GetProjects())
            {
                if (!project.TryGetDocument(documentFilePath, out _))
                {
                    results.Add(project);
                }
            }

            return results.DrainToImmutable();
        }
    }
}
