// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Capabilities;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Interfaces
{
    [Serial, Method(LanguageServerConstants.RazorSemanticTokensEndpoint)]
    internal interface ISemanticTokenHandler :
        IJsonRpcRequestHandler<SemanticTokenParams, SemanticTokens>,
        ICapability<SemanticTokensCapability>
    {
    }
}
