// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.ColorPresentation;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;

[Parallel, Method(ColorPresentationEndpoint.ColorPresentationMethodName)]
internal interface IColorPresentationEndpoint : IJsonRpcRequestHandler<ColorPresentationParamsBridge, ColorPresentation.ColorPresentation[]>,
    IRegistrationExtension
{
}
