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

#if !NET 
using System.Runtime.InteropServices;
using System.Security.Principal;
#endif

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal sealed class NamedPipeBasedRazorProjectInfoDriver : AbstractRazorProjectInfoDriver
{
    NamedPipeClientStream? _namedPipe;

    public NamedPipeBasedRazorProjectInfoDriver(ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        StartInitialization();
    }

    public Task ConnectAsync(string pipeName, CancellationToken cancellationToken)
    {
        Logger.LogTrace($"Connecting to named pipe {pipeName}");
        Debug.Assert(_namedPipe is null);

#if NET
        _namedPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous);
#else
        _namedPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.Asynchronous);
        CheckPipeConnectionOwnership(_namedPipe);
#endif

        return _namedPipe
            .ConnectAsync(cancellationToken)
            .ContinueWith(
                t => ReadFromStreamAsync(),
                TaskScheduler.Default);
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

#if !NET
    /// <summary>
    /// Check to ensure that the named pipe server we connected to is owned by the same
    /// user. From https://github.com/dotnet/roslyn/blob/5ec8cfd6d1255e4f4e9d610dd42aa30c60303e40/src/Compilers/Shared/NamedPipeUtil.cs#L132
    /// </summary>
    internal static bool CheckPipeConnectionOwnership(NamedPipeClientStream pipeStream)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var currentIdentity = WindowsIdentity.GetCurrent();
            var currentOwner = currentIdentity.Owner;
            var remotePipeSecurity = pipeStream.GetAccessControl();
            var remoteOwner = remotePipeSecurity.GetOwner(typeof(SecurityIdentifier));
            return currentOwner.Equals(remoteOwner);
        }

        // Client side validation isn't supported on Unix. The model relies on the server side
        // security here.
        return true;
    }
#endif
}
