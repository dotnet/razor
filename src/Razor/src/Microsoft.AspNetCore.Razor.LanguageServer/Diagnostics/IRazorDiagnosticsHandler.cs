// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics
{
    [Parallel, Method(LanguageServerConstants.RazorTranslateDiagnosticsEndpoint)]
    internal interface IRazorDiagnosticsHandler : IJsonRpcRequestHandler<RazorDiagnosticsParams, RazorDiagnosticsResponse>
    {
    }
}
