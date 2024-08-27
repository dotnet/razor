﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Razor.Telemetry;

namespace Microsoft.CodeAnalysis.Remote.Razor.ProjectSystem;

[Export(typeof(ProjectSnapshotFactory)), Shared]
[method: ImportingConstructor]
internal class ProjectSnapshotFactory(DocumentSnapshotFactory documentSnapshotFactory, ITelemetryReporter telemetryReporter)
{
    private static readonly ConditionalWeakTable<Project, RemoteProjectSnapshot> s_projectSnapshots = new();

    private readonly DocumentSnapshotFactory _documentSnapshotFactory = documentSnapshotFactory;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;

    public RemoteProjectSnapshot GetOrCreate(Project project)
    {
        lock (s_projectSnapshots)
        {
            if (!s_projectSnapshots.TryGetValue(project, out var projectSnapshot))
            {
                projectSnapshot = new RemoteProjectSnapshot(project, _documentSnapshotFactory, _telemetryReporter);
                s_projectSnapshots.Add(project, projectSnapshot);
            }

            return projectSnapshot;
        }
    }
}
