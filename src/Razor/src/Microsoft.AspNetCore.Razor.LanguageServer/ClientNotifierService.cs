// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class ClientNotifierService
    {
        private TaskCompletionSource<bool> _taskCompletionSource;
        private IClientLanguageServer _languageServer;

        public ClientNotifierService(IClientLanguageServer languageServer, TaskCompletionSource<bool> taskCompletionSource)
        {
            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            _languageServer = languageServer;
            _taskCompletionSource = taskCompletionSource;
        }

        public async Task<IResponseRouterReturns> SendRequestAsync(string method)
        {
            await _taskCompletionSource.Task;

            return _languageServer.SendRequest(method);
        }

        public async Task<IResponseRouterReturns> SendRequestAsync<T>(string method, T @params)
        {
            await _taskCompletionSource.Task;

            return _languageServer.SendRequest<T>(method, @params);
        }
    }
}
