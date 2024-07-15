// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal abstract partial class RazorBrokeredServiceBase : IDisposable
{
    private readonly IRazorServiceBroker _serviceBroker;

    protected readonly ILogger Logger;

    protected RazorBrokeredServiceBase(in ServiceArgs args)
    {
        _serviceBroker = args.ServiceBroker;

        var loggerFactory = args.ExportProvider.GetExportedValue<ILoggerFactory>();
        Logger = loggerFactory.GetOrCreateLogger(GetType());
    }

    protected ValueTask RunServiceAsync(Func<CancellationToken, ValueTask> implementation, CancellationToken cancellationToken)
        => _serviceBroker.RunServiceAsync(implementation, cancellationToken);

    protected ValueTask<T> RunServiceAsync<T>(RazorPinnedSolutionInfoWrapper solutionInfo, Func<Solution, ValueTask<T>> implementation, CancellationToken cancellationToken)
        => _serviceBroker.RunServiceAsync(solutionInfo, implementation, cancellationToken);

    public void Dispose()
    {
        _serviceBroker.Dispose();
    }
}
