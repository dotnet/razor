// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Cohost;

[Export(typeof(ProjectSnapshotFactory)), Shared]
[method: ImportingConstructor]
internal class ProjectSnapshotFactory(DocumentSnapshotFactory documentSnapshotFactory)
{
    private readonly DocumentSnapshotFactory _documentSnapshotFactory = documentSnapshotFactory;

    private readonly ConditionalWeakTable<Project, IProjectSnapshot> _projectSnapshots = new();

    public IProjectSnapshot GetOrCreate(Project project)
    {
        if (!_projectSnapshots.TryGetValue(project, out var projectSnapshot))
        {
            projectSnapshot = new CohostProjectSnapshot(project, _documentSnapshotFactory);
            _projectSnapshots.Add(project, projectSnapshot);
        }

        return projectSnapshot;
    }
}
