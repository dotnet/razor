// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Hosting;

[RazorLanguageServerEndpoint(CustomMessageNames.RazorNamedPipeConnectEndpointName)]
internal sealed class RazorNamedPipeConnectHandler(IRazorProjectInfoDriver infoDriver) : IRazorNotificationHandler<RazorConnectParams>
{
    private INamedPipeProjectInfoDriver _infoDriver = (infoDriver as INamedPipeProjectInfoDriver)
        ?? throw new InvalidOperationException($"The connection endpoint is only need in times where connection information is required for the project info driver.");

    public bool MutatesSolutionState => false;

    public Task HandleNotificationAsync(RazorConnectParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
        => _infoDriver.CreateNamedPipeAsync(request.PipeName, cancellationToken);
}
