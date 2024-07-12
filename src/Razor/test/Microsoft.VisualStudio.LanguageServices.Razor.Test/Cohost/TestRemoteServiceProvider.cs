// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common.VisualStudio;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Composition;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Test.Cohost;

/// <summary>
/// An implementation of IRemoteServiceProvider that doesn't actually do anything remote, but rather directly calls service methods
/// </summary>
internal class TestRemoteServiceProvider(ExportProvider exportProvider) : IRemoteServiceProvider
{
    private static readonly Dictionary<Type, IServiceHubServiceFactory> s_factoryMap = BuildFactoryMap();

    private readonly IServiceProvider _serviceProvider = VsMocks.CreateServiceProvider(b => b.AddService<TraceSource>(serviceInstance: null));

    private static Dictionary<Type, IServiceHubServiceFactory> BuildFactoryMap()
    {
        var result = new Dictionary<Type, IServiceHubServiceFactory>();

        foreach (var type in typeof(RazorServiceFactoryBase<>).Assembly.GetTypes())
        {
            if (!type.IsAbstract &&
                typeof(IServiceHubServiceFactory).IsAssignableFrom(type))
            {
                Assert.Equal(typeof(RazorServiceFactoryBase<>), type.BaseType.GetGenericTypeDefinition());

                var genericType = type.BaseType.GetGenericArguments().FirstOrDefault();
                if (genericType != null)
                {
                    // ServiceHub requires a parameterless constructor, so we can safely rely on it existing too
                    var factory = (IServiceHubServiceFactory)Activator.CreateInstance(type);
                    result.Add(genericType, factory);
                }
            }
        }

        return result;
    }

    public async ValueTask<TResult?> TryInvokeAsync<TService, TResult>(
        Solution solution,
        Func<TService, RazorPinnedSolutionInfoWrapper, CancellationToken, ValueTask<TResult>> invocation,
        CancellationToken cancellationToken,
        [CallerFilePath] string? callerFilePath = null,
        [CallerMemberName] string? callerMemberName = null)
        where TService : class, IDisposable
    {
        Assert.True(s_factoryMap.TryGetValue(typeof(TService), out var factory));

        var testServiceBroker = new TestServiceBroker(solution);

        var serviceFactory = (RazorServiceFactoryBase<TService>)factory;
        using var service = serviceFactory.GetTestAccessor().CreateService(testServiceBroker, exportProvider);

        // This is never used, we short-circuited things by passing the solution direct to the TestServiceBroker
        var solutionInfo = new RazorPinnedSolutionInfoWrapper();
        return await invocation(service, solutionInfo, cancellationToken);
    }
}
