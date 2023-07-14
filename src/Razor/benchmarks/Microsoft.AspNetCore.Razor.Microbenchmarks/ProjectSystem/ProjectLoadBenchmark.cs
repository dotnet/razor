﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks;

public class ProjectLoadBenchmark : ProjectSnapshotManagerBenchmarkBase
{
    [IterationSetup]
    public void Setup()
    {
        SnapshotManager = CreateProjectSnapshotManager();
    }

    private DefaultProjectSnapshotManager SnapshotManager { get; set; }

    [Benchmark(Description = "Initializes a project and 100 files", OperationsPerInvoke = 100)]
    public void ProjectLoad_AddProjectAnd100Files()
    {
        SnapshotManager.ProjectAdded(HostProject);

        for (var i= 0; i < Documents.Length; i++)
        {
            SnapshotManager.DocumentAdded(HostProject.Key, Documents[i], TextLoaders[i % 4]);
        }
    }
}
