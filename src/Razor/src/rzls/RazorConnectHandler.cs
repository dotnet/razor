// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

#pragma warning disable RS0035 // External access to internal symbols outside the restricted namespace(s) is prohibited
[RazorLanguageServerEndpoint(CustomMessageNames.RazorConnectEndpointName)]
internal sealed class RazorConnectHandler(IConnectionBasedRazorProjectInfoDriver<string> infoDriver) : IRazorNotificationHandler<RazorConnectParams>
{
    public bool MutatesSolutionState => false;

    public Task HandleNotificationAsync(RazorConnectParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
        => infoDriver.ConnectAsync(request.PipeName, cancellationToken);
}
#pragma warning restore RS0035 // External access to internal symbols outside the restricted namespace(s) is prohibited
