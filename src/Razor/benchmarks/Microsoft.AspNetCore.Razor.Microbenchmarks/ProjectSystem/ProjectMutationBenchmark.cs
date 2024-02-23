// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
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
    }

    private Thread _addRemoveThread;
    private Thread _readThread;

    [IterationSetup]
    public void Setup()
    {
        ProjectManager = CreateProjectSnapshotManager();
    }

    private DefaultProjectSnapshotManager ProjectManager { get; set; }

    [Benchmark(Description = "Does thread contention add/remove of documents", OperationsPerInvoke = 100)]
    public async Task ProjectMutation_Mutates100kFilesAsync()
    {
        await Dispatcher.RunOnDispatcherThreadAsync(() =>
        {
            ProjectManager.ProjectAdded(HostProject);
        }, CancellationToken.None);

        var cancellationSource = new CancellationTokenSource();
        var done = false;

#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
        _addRemoveThread = new Thread(async () =>
        {
            for (var i = 0; i < Documents.Length; i++)
            {
                var document = Documents[i];
                await Dispatcher.RunOnDispatcherThreadAsync(() => ProjectManager.DocumentAdded(HostProject.Key, document, TextLoaders[i % 4]), CancellationToken.None).ConfigureAwait(false);
                Thread.Sleep(0);
                await Dispatcher.RunOnDispatcherThreadAsync(() => ProjectManager.DocumentRemoved(HostProject.Key, document), CancellationToken.None).ConfigureAwait(false);
                Thread.Sleep(0);
            }

            cancellationSource.Cancel();
        });

        _readThread = new Thread(async () =>
        {
            while (true)
            {
                if (cancellationSource.IsCancellationRequested)
                {
                    done = true;
                    return;
                }

                await Dispatcher.RunOnDispatcherThreadAsync(() => ProjectManager.GetProjects(), CancellationToken.None).ConfigureAwait(false);
                Thread.Sleep(0);
                await Dispatcher.RunOnDispatcherThreadAsync(() => ProjectManager.GetOpenDocuments(), CancellationToken.None).ConfigureAwait(false);
                Thread.Sleep(0);
            }
        });

        _addRemoveThread.Start();
        _readThread.Start();
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates

        while (!done)
        {
            Thread.Sleep(0);
        }
    }
}
