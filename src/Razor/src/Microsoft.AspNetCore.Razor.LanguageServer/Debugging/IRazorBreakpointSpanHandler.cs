// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Debugging
{
    [Parallel, Method(LanguageServerConstants.RazorBreakpointSpanEndpoint)]
    internal interface IRazorBreakpointSpanHandler : IJsonRpcRequestHandler<RazorBreakpointSpanParams, RazorBreakpointSpanResponse?>
    {
    }
}
