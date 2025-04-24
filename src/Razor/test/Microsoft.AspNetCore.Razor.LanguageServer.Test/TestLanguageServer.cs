// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test;

internal class TestLanguageServer(Dictionary<string, Func<object?, Task<object>>> requestResponseFactory) : IClientConnection
{
    private readonly Dictionary<string, Func<object?, Task<object>>> _requestResponseFactory = requestResponseFactory;

    public Task SendNotificationAsync<TParams>(string method, TParams @params, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task SendNotificationAsync(string method, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public async Task<TResponse> SendRequestAsync<TParams, TResponse>(string method, TParams @params, CancellationToken cancellationToken)
    {
        Assert.True(_requestResponseFactory.TryGetValue(method, out var factory), $"No request factory setup for {method}");

        return (TResponse)await factory(@params);
    }
}
