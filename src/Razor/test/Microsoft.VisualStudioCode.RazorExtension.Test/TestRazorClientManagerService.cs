// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.VisualStudioCode.RazorExtension.Test;

internal class TestRazorClientLanguageServerManager : IRazorClientLanguageServerManager
{
    private readonly ConcurrentStack<(string, object?)> _requests = new();

    public ValueTask SendNotificationAsync(string methodName, CancellationToken cancellationToken)
    {
        _requests.Push((methodName, null));
        return new ValueTask(Task.CompletedTask);
    }

    public ValueTask SendNotificationAsync<TParams>(string methodName, TParams @params, CancellationToken cancellationToken)
    {
        _requests.Push((methodName, @params));
        return new ValueTask(Task.CompletedTask);
    }

    public ValueTask SendRequestAsync(string methodName, CancellationToken cancellationToken)
    {
        _requests.Push((methodName, null));
        return new ValueTask(Task.CompletedTask);
    }

    public Task<TResponse> SendRequestAsync<TParams, TResponse>(string methodName, TParams @params, CancellationToken cancellationToken)
    {
        _requests.Push((methodName, @params));
        return Task.FromResult<TResponse>(default!);
    }

    public ValueTask SendRequestAsync<TParams>(string methodName, TParams @params, CancellationToken cancellationToken)
    {
        _requests.Push((methodName, @params));
        return new ValueTask(Task.CompletedTask);
    }

    // Helper method to retrieve recorded requests for testing
    public (string MethodName, object? Parameters)[] GetRequests()
    {
        return _requests.ToArray();
    }
}
