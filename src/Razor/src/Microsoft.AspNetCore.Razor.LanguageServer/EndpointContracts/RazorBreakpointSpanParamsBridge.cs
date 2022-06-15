// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Protocol;

internal class RazorBreakpointSpanParamsBridge : RazorBreakpointSpanParams, IRequest<RazorBreakpointSpanResponse>
{
}
