﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks;

public class BackgroundCodeGenerationBenchmark : ProjectSnapshotManagerBenchmarkBase
{
    [IterationSetup]
    public void Setup()
    {
        SnapshotManager = CreateProjectSnapshotManager();
        SnapshotManager.ProjectAdded(HostProject);
        SnapshotManager.Changed += SnapshotManager_Changed;
    }

    [IterationCleanup]
    public void Cleanup()
    {
        SnapshotManager.Changed -= SnapshotManager_Changed;

        Tasks.Clear();
    }

    private List<Task> Tasks { get; } = new List<Task>();

    private DefaultProjectSnapshotManager SnapshotManager { get; set; }

    [Benchmark(Description = "Generates the code for 100 files", OperationsPerInvoke = 100)]
    public async Task BackgroundCodeGeneration_Generate100FilesAsync()
    {
        for (var i = 0; i < Documents.Length; i++)
        {
            SnapshotManager.DocumentAdded(HostProject.Key, Documents[i], TextLoaders[i % 4]);
        }

        await Task.WhenAll(Tasks);
    }

    private void SnapshotManager_Changed(object sender, ProjectChangeEventArgs e)
    {
        // The real work happens here.
        var project = SnapshotManager.GetLoadedProject(e.ProjectKey);
        var document = project.GetDocument(e.DocumentFilePath);

        Tasks.Add(document.GetGeneratedOutputAsync());
    }
}
