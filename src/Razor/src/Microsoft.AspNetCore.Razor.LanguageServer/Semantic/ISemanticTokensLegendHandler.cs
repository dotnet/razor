// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    [Method(LanguageServerConstants.RazorSemanticTokensLegendEndpoint)]
    internal interface ISemanticTokensLegendHandler :
        IJsonRpcRequestHandler<SemanticTokensLegendParams, SemanticTokensLegend>
    {
    }
}
