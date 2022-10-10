// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Serialization;
using StreamJsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal sealed class RazorLanguageServerWrapper : IAsyncDisposable
{
    private readonly RazorLanguageServer _innerServer;
    private readonly object _disposeLock;
    private bool _disposed;

    private RazorLanguageServerWrapper(RazorLanguageServer innerServer)
    {
        if (innerServer is null)
        {
            throw new ArgumentNullException(nameof(innerServer));
        }

        _innerServer = innerServer;
        _disposeLock = new object();
    }

    public static RazorLanguageServerWrapper Create(
        Stream input,
        Stream output,
        IRazorLogger razorLogger,
        ProjectSnapshotManagerDispatcher? projectSnapshotManagerDispatcher = null,
        Action<IServiceCollection>? configure = null,
        LanguageServerFeatureOptions? featureOptions = null)
    {
        var jsonRpc = CreateJsonRpc(input, output);

        var server = new RazorLanguageServer(
            jsonRpc,
            razorLogger,
            projectSnapshotManagerDispatcher,
            featureOptions,
            configure);

        var razorLanguageServer = new RazorLanguageServerWrapper(server);
        jsonRpc.StartListening();

        return razorLanguageServer;
    }

    private static JsonRpc CreateJsonRpc(Stream input, Stream output)
    {
        var messageFormatter = new JsonMessageFormatter();
        messageFormatter.JsonSerializer.AddVSInternalExtensionConverters();
        messageFormatter.JsonSerializer.Converters.RegisterRazorConverters();
        messageFormatter.JsonSerializer.ContractResolver = new CamelCasePropertyNamesContractResolver();

        var jsonRpc = new JsonRpc(new HeaderDelimitedMessageHandler(output, input, messageFormatter));

        return jsonRpc;
    }

    public Task WaitForExitAsync()
    {
        var lifeCycleManager = GetRequiredService<RazorLifeCycleManager>();

        return lifeCycleManager.WaitForExit;
    }

    internal T GetRequiredService<T>() where T : notnull
    {
        return _innerServer.GetRequiredService<T>();
    }

    public async ValueTask DisposeAsync()
    {
        await _innerServer.DisposeAsync();

        lock (_disposeLock)
        {
            if (!_disposed)
            {
                _disposed = true;

                TempDirectory.Instance.Dispose();
            }
        }
    }

    internal RazorLanguageServer GetInnerLanguageServerForTesting()
    {
        return _innerServer;
    }
}
