// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Legend
{
    [Parallel, Method(LanguageServerConstants.RazorSemanticTokensLegendEndpoint)]
    internal interface IRazorSemanticTokensLegendHandler : IJsonRpcRequestHandler<SemanticTokensLegendParams, SemanticTokensLegend>
    {
    }
}
