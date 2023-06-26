// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks;

public class ProjectMutationBenchmark : ProjectSnapshotManagerBenchmarkBase
{
    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    public ProjectMutationBenchmark()
        : base(100000)
    {
        _dispatcher = new SnapshotDispatcher(nameof(ProjectMutationBenchmark));
    }

    [IterationSetup]
    public void Setup()
    {
        SnapshotManager = CreateProjectSnapshotManager(_dispatcher);
    }

    private DefaultProjectSnapshotManager SnapshotManager { get; set; }

    [Benchmark(Description = "Does thread contention add/remove of documents", OperationsPerInvoke = 100)]
    public async Task ProjectMutation_Mutates100kFiles()
    {
        await _dispatcher.RunOnDispatcherThreadAsync(() =>
        {
            SnapshotManager.ProjectAdded(HostProject);
        }, CancellationToken.None);

        var tasks = new Task[Documents.Length * 2];

        for (var i = 0; i < tasks.Length; i += 2)
        {
            tasks[i] = AddAndRemoveAsync(i / 2);
            tasks[i+1] = GetDocumentsAsync();
        }

        await Task.WhenAll(tasks);
    }

    private async Task AddAndRemoveAsync(int i)
    {
        await _dispatcher.RunOnDispatcherThreadAsync(() =>
        {
            SnapshotManager.DocumentAdded(HostProject, Documents[i], TextLoaders[i % 4]);
        }, CancellationToken.None);

        await _dispatcher.RunOnDispatcherThreadAsync(() =>
        {
            SnapshotManager.DocumentRemoved(HostProject, Documents[i]);
        }, CancellationToken.None);
    }

    private Task GetDocumentsAsync() =>
        _dispatcher.RunOnDispatcherThreadAsync(() => SnapshotManager.Projects, CancellationToken.None);

    private class SnapshotDispatcher : ProjectSnapshotManagerDispatcherBase
    {
        public SnapshotDispatcher(string threadName) : base(threadName)
        {
        }

        public override void LogException(Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
        }
    }
}
