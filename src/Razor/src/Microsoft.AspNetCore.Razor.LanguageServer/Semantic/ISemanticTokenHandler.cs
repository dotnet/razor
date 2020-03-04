// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    [Method("razor/semanticTokens")]
    [Parallel]
    internal interface ISemanticTokenHandler :
        IJsonRpcRequestHandler<SemanticTokenParams, SemanticTokens>,
        IRequestHandler<SemanticTokenParams, SemanticTokens>,
        IJsonRpcHandler,
        ICapability<SemanticTokenCapability>
    {
        
    }
}
