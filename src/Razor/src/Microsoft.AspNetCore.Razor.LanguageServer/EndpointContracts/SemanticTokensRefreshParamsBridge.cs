// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using MediatR;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    /// <summary>
    /// This class is used as a "bridge" between the O# and VS worlds. Ultimately it only exists because the base <see cref="VSInternalCompletionItem"/>
    /// type does not implement <see cref="IRequest{TResponse}"/>.
    /// </summary>
    internal class SemanticTokensRefreshParamsBridge : IRequest
    { }
}
