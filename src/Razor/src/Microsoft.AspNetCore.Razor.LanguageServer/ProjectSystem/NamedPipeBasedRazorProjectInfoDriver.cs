// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal class NamedPipeBasedRazorProjectInfoDriver : AbstractRazorProjectInfoDriver
{
    NamedPipeClientStream? _namedPipe;
    Task? _readTask;

    public NamedPipeBasedRazorProjectInfoDriver(ILoggerFactory loggerFactory) : base(loggerFactory, TimeSpan.FromMilliseconds(200))
    {
        StartInitialization();
    }

    public async Task ConnectAsync(string pipeName, CancellationToken cancellationToken)
    {
        Logger.LogTrace($"Connecting to named pipe {pipeName}");
        Debug.Assert(_namedPipe is null);

#if NET
        _namedPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.CurrentUserOnly);
#else
        _namedPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
#endif

        await _namedPipe.ConnectAsync(cancellationToken).ConfigureAwait(false);

        _readTask = Task.Run(() => ReadFromStreamAsync(), cancellationToken);
    }

    protected override Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ReadFromStreamAsync(CancellationToken cancellationToken = default)
    {
        _namedPipe.AssumeNotNull();

        Logger?.LogTrace($"Starting read from named pipe.");

        while (_namedPipe.IsConnected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                switch (_namedPipe.ReadProjectInfoAction())
                {
                    case ProjectInfoAction.Remove:
                        Logger?.LogTrace($"Attempting to read project id for removal");
                        var id = await _namedPipe.ReadProjectInfoRemovalAsync(cancellationToken).ConfigureAwait(false);
                        EnqueueRemove(new ProjectKey(id));

                        break;

                    case ProjectInfoAction.Update:
                        Logger?.LogTrace($"Attempting to read project info for update");
                        var projectInfo = await _namedPipe.ReadProjectInfoAsync(cancellationToken).ConfigureAwait(false);
                        if (projectInfo is not null)
                        {
                            EnqueueUpdate(projectInfo);
                        }

                        break;

                    default:
                        throw Assumes.NotReachable();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"{ex.Message}");
            }
        }
    }
}
