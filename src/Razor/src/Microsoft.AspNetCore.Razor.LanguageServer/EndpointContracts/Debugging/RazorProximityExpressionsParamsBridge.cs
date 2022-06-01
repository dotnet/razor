// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts.Debugging;

internal class RazorProximityExpressionsParamsBridge : RazorProximityExpressionsParams, IRequest<RazorProximityExpressionsResponse>
{
}
