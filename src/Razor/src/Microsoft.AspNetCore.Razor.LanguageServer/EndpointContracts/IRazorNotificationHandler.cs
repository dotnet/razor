// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CommonLanguageServerProtocol.Framework;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts
{
    internal interface IRazorNotificationHandler<RequestType> : INotificationHandler<RequestType, RazorRequestContext>
    {
    }

    internal interface IRazorNotificationHandler : INotificationHandler<RazorRequestContext>
    {
    }
}
