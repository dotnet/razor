// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Endpoints;

internal class UpdateLogLevelEndpoint(LogLevelProvider logLevelProvider) : IRazorNotificationHandler<UpdateLogLevelParams>
{
    public bool MutatesSolutionState => false;

    public Task HandleNotificationAsync(UpdateLogLevelParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var logLevel = Enum.Parse<LogLevel>(request.LogLevelValue);
        logLevelProvider.SetLogLevel(logLevel);

        return Task.CompletedTask;
    }
}
