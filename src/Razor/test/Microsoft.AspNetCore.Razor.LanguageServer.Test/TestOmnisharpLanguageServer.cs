// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using InitializeParams = OmniSharp.Extensions.LanguageServer.Protocol.Models.InitializeParams;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test
{
    internal class TestOmnisharpLanguageServer : ClientNotifierServiceBase
    {
        private readonly IReadOnlyDictionary<string, Func<object, object>> _requestResponseFactory;

        public TestOmnisharpLanguageServer(Dictionary<string, Func<object, object>> requestResponseFactory)
        {
            _requestResponseFactory = requestResponseFactory;
        }

        public override InitializeParams ClientSettings => throw new NotImplementedException();

        public override Task OnStarted(ILanguageServer server, CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task<IResponseRouterReturns> SendRequestAsync(string method)
        {
            return SendRequestAsync(method, @params: (object)null);
        }

        public override Task<IResponseRouterReturns> SendRequestAsync<TParams>(string method, TParams @params)
        {
            if (!_requestResponseFactory.TryGetValue(method, out var factory))
            {
                throw new InvalidOperationException($"No request factory setup for {method}");
            }

            var result = factory(@params);
            return Task.FromResult<IResponseRouterReturns>(new TestResponseRouterReturns(result));
        }

        private class TestResponseRouterReturns : IResponseRouterReturns
        {
            private readonly object _result;

            public TestResponseRouterReturns(object result)
            {
                _result = result;
            }

            public Task<Response> Returning<Response>(CancellationToken cancellationToken)
            {
                return Task.FromResult((Response)_result);
            }

            public Task ReturningVoid(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
