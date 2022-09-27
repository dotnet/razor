// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.ColorPresentation;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;

internal class ColorPresentationParamsBridge : ColorPresentationParams, IRequest<ColorPresentation.ColorPresentation[]>
{
}
