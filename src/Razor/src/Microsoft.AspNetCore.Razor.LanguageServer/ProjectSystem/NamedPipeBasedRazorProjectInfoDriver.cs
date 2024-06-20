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
    NamedPipeClientStream? _stream;
    Task? _readTask;

    public NamedPipeBasedRazorProjectInfoDriver(ILoggerFactory loggerFactory) : base(loggerFactory, TimeSpan.FromMilliseconds(200))
    {
        StartInitialization();
    }

    public async Task ConnectAsync(string pipeName, CancellationToken cancellationToken)
    {
        Logger.LogTrace($"Connecting to named pipe {pipeName}");
        Debug.Assert(_stream is null);

#if NET
        _stream = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.CurrentUserOnly);
#else
        _stream = new NamedPipeClientStream(".", pipeName, PipeDirection.In);
#endif

        await _stream.ConnectAsync(cancellationToken).ConfigureAwait(false);

        _readTask = Task.Run(ReadFromStreamAsync);
    }

    protected override Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ReadFromStreamAsync()
    {
        _stream.AssumeNotNull();

        Logger?.LogTrace($"Starting read from named pipe.");

        var sizeBuffer = new byte[sizeof(int)];
        while (_stream.IsConnected)
        {
            try
            {
                var actionByte = _stream.ReadByte();
                if (actionByte == -1)
                {
                    Logger?.LogTrace($"Named pipe is closed");
                    return;
                }

                if (actionByte == 0)
                {
                    Logger?.LogTrace($"Attempting to read project id for removal");
                    var id = await _stream.ReadStringAsync().ConfigureAwait(false);
                    EnqueueRemove(new ProjectKey(id));
                }
                else
                {
                    Logger?.LogTrace($"Attempting to read project info for update");
                    await _stream.ReadAsync(sizeBuffer, 0, sizeBuffer.Length).ConfigureAwait(false);

                    var sizeToRead = BitConverter.ToUInt32(sizeBuffer, 0);
                    Logger?.LogTrace($"Reading {sizeToRead} bytes of project info");
                    var projectInfoBuffer = new byte[sizeToRead];

                    await _stream.ReadAsync(projectInfoBuffer, 0, projectInfoBuffer.Length).ConfigureAwait(false);
                    var projectInfo = TryDeserialize(projectInfoBuffer);

                    Logger?.LogTrace($"Deserialized info for: {projectInfo?.FilePath ?? "null"}");
                    if (projectInfo is not null)
                    {
                        EnqueueUpdate(projectInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, $"{ex.Message}");
            }
        }
    }

    private RazorProjectInfo? TryDeserialize(byte[] bytes)
    {
        try
        {
            return RazorProjectInfo.DeserializeFrom(bytes.AsMemory());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error occurred while reading and deserializing");
        }

        return null;
    }
}
