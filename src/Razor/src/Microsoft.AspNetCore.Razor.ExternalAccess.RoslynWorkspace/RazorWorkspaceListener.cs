// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO.Pipes;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.RoslynWorkspace;

public sealed class RazorWorkspaceListener : RazorWorkspaceListenerBase
{
    public RazorWorkspaceListener(ILoggerFactory loggerFactory) : base(loggerFactory.CreateLogger(nameof(RazorWorkspaceListener)))
    { 
    }

    public void EnsureInitialized(Workspace workspace, string pipeName)
    {
        EnsureInitialized(workspace, () => new NamedPipeServerStream(
                pipeName,
                PipeDirection.Out,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.CurrentUserOnly | PipeOptions.Asynchronous));
    }

    private protected override Task CheckConnectionAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is NamedPipeServerStream { IsConnected: false } namedPipe)
        {
            return namedPipe.WaitForConnectionAsync(cancellationToken);
        }

        return Task.CompletedTask;
    }
}
