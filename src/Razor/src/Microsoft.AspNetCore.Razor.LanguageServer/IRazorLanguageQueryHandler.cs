// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    [Parallel, Method(LanguageServerConstants.RazorLanguageQueryEndpoint)]
    internal interface IRazorLanguageQueryHandler : IJsonRpcRequestHandler<RazorLanguageQueryParams, RazorLanguageQueryResponse>
    {
    }
}
