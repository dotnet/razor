// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class ClientNotifierService
    {
        private TaskCompletionSource<bool> _taskCompletionSource;

        public ClientNotifierService(TaskCompletionSource<bool> taskCompletionSource)
        {
            _taskCompletionSource = taskCompletionSource;
        }

        public async Task<IResponseRouterReturns> SendRequestAsync(IClientLanguageServer languageServer, string method)
        {
            await _taskCompletionSource.Task;

            return languageServer.SendRequest(method);
        }

        public async Task<IResponseRouterReturns> SendRequestAsync<T>(IClientLanguageServer languageServer, string method, T @params)
        {
            await _taskCompletionSource.Task;

            return languageServer.SendRequest<T>(method, params);
        }
    }
}
