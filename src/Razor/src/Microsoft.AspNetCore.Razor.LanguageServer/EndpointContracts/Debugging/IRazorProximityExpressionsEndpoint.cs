// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts.Debugging;

[Parallel, Method(LanguageServerConstants.RazorProximityExpressionsEndpoint)]
internal interface IRazorProximityExpressionsEndpoint : IJsonRpcRequestHandler<RazorProximityExpressionsParamsBridge, RazorProximityExpressionsResponse?>
{
}
