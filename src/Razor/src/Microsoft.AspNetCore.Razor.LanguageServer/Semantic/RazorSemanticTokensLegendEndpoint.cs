// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class SemanticTokensLegendParams : IRequest<SemanticTokensLegend>, IBaseRequest
    {
    }

    [Method(LanguageServerConstants.RazorSemanticTokensLegendEndpoint)]
    internal interface ISemanticTokensLegendHandler :
        IJsonRpcRequestHandler<SemanticTokensLegendParams, SemanticTokensLegend>
    {
    }

    internal class RazorSemanticTokensLegendEndpoint : ISemanticTokensLegendHandler
    {
        public Task<SemanticTokensLegend> Handle(SemanticTokensLegendParams request, CancellationToken cancellationToken)
        {
            return Task.FromResult(RazorSemanticTokensLegend.Instance);
        }
    }
}
