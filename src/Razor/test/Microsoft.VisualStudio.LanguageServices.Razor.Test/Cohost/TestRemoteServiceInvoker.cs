// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor;
using Microsoft.VisualStudio.Composition;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

/// <summary>
/// An implementation of <see cref="IRemoteServiceInvoker"/> that doesn't actually do anything remote,
/// but rather directly calls service methods
/// </summary>
internal sealed class TestRemoteServiceInvoker(ExportProvider exportProvider) : IRemoteServiceInvoker, IDisposable
{
    private readonly TestServiceBroker _serviceBroker = new();
    private readonly Dictionary<Type, IDisposable> _services = [];

    private TService GetOrCreateService<TService>()
        where TService : class, IDisposable
    {
        if (!_services.TryGetValue(typeof(TService), out var service))
        {
            var args = new ServiceArgs(_serviceBroker, exportProvider);
            service = BrokeredServiceFactory.CreateService<TService>(in args);
            _services.Add(typeof(TService), service);
        }

        return (TService)service;
    }

    public async ValueTask<TResult?> TryInvokeAsync<TService, TResult>(
        Solution solution,
        Func<TService, RazorPinnedSolutionInfoWrapper, CancellationToken, ValueTask<TResult>> invocation,
        CancellationToken cancellationToken,
        [CallerFilePath] string? callerFilePath = null,
        [CallerMemberName] string? callerMemberName = null)
        where TService : class, IDisposable
    {
        var service = GetOrCreateService<TService>();

        // In an ideal world we'd be able to maintain a dictionary of solution checksums in TestServiceBroker, and use
        // the RazorPinnedSolutionInfoWrapper properly, but we need Roslyn changes for that. For now, this works fine
        // as we don't have any code that makes multiple parallel calls to TryInvokeAsync in the same test.
        var solutionInfo = new RazorPinnedSolutionInfoWrapper();
        _serviceBroker.UpdateSolution(solution);
        return await invocation(service, solutionInfo, cancellationToken);
    }

    public void Dispose()
    {
        foreach (var service in _services.Values)
        {
            service.Dispose();
        }
    }
}
