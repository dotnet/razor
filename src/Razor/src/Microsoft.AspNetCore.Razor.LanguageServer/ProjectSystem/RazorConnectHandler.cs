// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

[RazorLanguageServerEndpoint(CustomMessageNames.RazorConnectEndpointName)]
internal sealed class RazorConnectHandler(NamedPipeBasedRazorProjectInfoDriver infoDriver) : IRazorNotificationHandler<RazorConnectParams>
{
    public bool MutatesSolutionState => false;

    public Task HandleNotificationAsync(RazorConnectParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        infoDriver.ConnectAsync(request.PipeName, cancellationToken).Forget();
        return Task.CompletedTask;
    }
}
