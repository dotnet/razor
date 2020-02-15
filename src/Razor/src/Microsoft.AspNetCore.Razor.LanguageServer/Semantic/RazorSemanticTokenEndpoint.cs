// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class SemanticTokenParams : IRequest<SemanticToken>, IBaseRequest
    {

    }

    public class SemanticToken
    {

    }

    public class SemanticTokenCapability : DynamicCapability
    {

    }

    [Method("razor/semanticTokens")]
    [Parallel]
    internal interface ISemanticTokenHandler :
        IJsonRpcRequestHandler<SemanticTokenParams, SemanticToken>,
        IRequestHandler<SemanticTokenParams, SemanticToken>,
        IJsonRpcHandler,
        ICapability<SemanticTokenCapability>
    {

    }

    public class RazorSemanticTokenEndpoint : ISemanticTokenHandler
    {
        private SemanticTokenCapability _capability;

        public Task<SemanticToken> Handle(SemanticTokenParams request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public void SetCapability(SemanticTokenCapability capability)
        {
            _capability = capability;
        }
    }
}
