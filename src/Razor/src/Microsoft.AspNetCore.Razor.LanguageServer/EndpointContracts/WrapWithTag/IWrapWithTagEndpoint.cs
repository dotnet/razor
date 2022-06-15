// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts.WrapWithTag;

[Parallel, Method(LanguageServerConstants.RazorWrapWithTagEndpoint)]
internal interface IWrapWithTagEndpoint : IJsonRpcRequestHandler<WrapWithTagParamsBridge, WrapWithTagResponse?>
{
}
