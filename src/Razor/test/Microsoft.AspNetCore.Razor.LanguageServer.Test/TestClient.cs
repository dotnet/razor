// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Progress;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class TestClient : IClientLanguageServer
    {
        public TestClient(InitializeParams clientSettings)
        {
            ClientSettings = clientSettings;
        }

        public TestClient()
        {
            ClientSettings = new InitializeParams();
        }

        private readonly List<UpdateBufferRequest> _updateRequests = new();

        private readonly List<RequestPair> _requests = new();

        public IReadOnlyList<UpdateBufferRequest> UpdateRequests => _updateRequests;

        public IReadOnlyList<RequestPair> Requests => _requests;

        public IProgressManager ProgressManager => throw new NotImplementedException();

        public IServerWorkDoneManager WorkDoneManager => throw new NotImplementedException();

        public ILanguageServerConfiguration Configuration => throw new NotImplementedException();

        public InitializeParams ClientSettings { get; }

        public InitializeResult ServerSettings => throw new NotImplementedException();

        public void SendNotification(string method) => throw new NotImplementedException();

        public void SendNotification<T>(string method, T @params) => throw new NotImplementedException();

        public void SendNotification(IRequest request)
        {
            throw new NotImplementedException();
        }

        IResponseRouterReturns IResponseRouter.SendRequest<T>(string method, T @params)
        {
            if (@params is UpdateBufferRequest updateRequest)
            {
                _updateRequests.Add(updateRequest);
            }
            else
            {
                _requests.Add(new RequestPair(method, @params));
            }

            return GetResponseRouterReturns();
        }

        public IResponseRouterReturns SendRequest(string method)
        {
            _requests.Add(new RequestPair(method, Params: null));

            return GetResponseRouterReturns();
        }

        public Task<TResponse> SendRequest<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public object GetService(Type serviceType)
        {
            throw new NotImplementedException();
        }

        public bool TryGetRequest(long id, out string method, out TaskCompletionSource<JToken> pendingTask)
        {
            throw new NotImplementedException();
        }

        private IResponseRouterReturns GetResponseRouterReturns()
        {
            var mock = new Mock<IResponseRouterReturns>(MockBehavior.Strict);
            mock.Setup(r => r.ReturningVoid(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            return mock.Object;
        }

        internal record RequestPair(string Method, object? Params);
    }
}
