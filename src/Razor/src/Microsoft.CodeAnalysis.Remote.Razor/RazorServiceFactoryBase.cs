// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;
using Microsoft.VisualStudio.Composition;
using Nerdbank.Streams;

namespace Microsoft.CodeAnalysis.Remote.Razor;

/// <summary>
/// </summary>
/// <typeparam name="TService"></typeparam>
/// <remarks>
/// Implementors of <see cref="IServiceHubServiceFactory" /> (and thus this class) MUST provide a parameterless constructor or ServiceHub will fail to construct them.
/// </remarks>
internal abstract partial class RazorServiceFactoryBase<TService> : IServiceHubServiceFactory where TService : class
{
    private readonly RazorServiceDescriptorsWrapper _razorServiceDescriptors;
    private ITelemetryReporter? _telemetryReporter;

    /// <summary>
    /// </summary>
    /// <param name="razorServiceDescriptors"></param>
    /// <remarks>
    /// Implementors of <see cref="IServiceHubServiceFactory" /> (and thus this class) MUST provide a parameterless constructor or ServiceHub will fail to construct them.
    /// </remarks>
    public RazorServiceFactoryBase(RazorServiceDescriptorsWrapper razorServiceDescriptors)
    {
        _razorServiceDescriptors = razorServiceDescriptors;
    }

    public Task<object> CreateAsync(
       Stream stream,
       IServiceProvider hostProvidedServices,
       ServiceActivationOptions serviceActivationOptions,
       IServiceBroker serviceBroker,
       AuthorizationServiceClient? authorizationServiceClient)
    {
        // Dispose the AuthorizationServiceClient since we won't be using it
        authorizationServiceClient?.Dispose();

        _telemetryReporter = (ITelemetryReporter)hostProvidedServices.GetService(typeof(ITelemetryReporter));

        return Task.FromResult((object)Create(stream.UsePipe(), serviceBroker));
    }

    internal TService Create(IDuplexPipe pipe, IServiceBroker serviceBroker)
    {
        var descriptor = _razorServiceDescriptors.GetDescriptorForServiceFactory(typeof(TService));
        var serverConnection = descriptor.ConstructRpcConnection(pipe);

        var service = CreateService(serviceBroker,
            // TODO: This seems to always be null. Suspect this was intended to be something different.
            _telemetryReporter ?? NoOpTelemetryReporter.Instance,
            _exportProvider);

        serverConnection.AddLocalRpcTarget(service);
        serverConnection.StartListening();

        return service;
    }

    protected abstract TService CreateService(IServiceBroker serviceBroker, ITelemetryReporter telemetryReporter, ExportProvider exportProvider);
}
