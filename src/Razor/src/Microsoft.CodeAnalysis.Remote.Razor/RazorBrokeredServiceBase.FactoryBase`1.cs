// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Remote.Razor.Logging;
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;
using Nerdbank.Streams;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal abstract partial class RazorBrokeredServiceBase
{
    /// <remarks>
    /// Implementors of <see cref="IServiceHubServiceFactory" /> (and thus this class) MUST provide a parameterless constructor
    /// or ServiceHub will fail to construct them.
    /// </remarks>
    internal abstract class FactoryBase<TService> : IServiceHubServiceFactory
        where TService : class
    {
        public async Task<object> CreateAsync(
            Stream stream,
            IServiceProvider hostProvidedServices,
            ServiceActivationOptions serviceActivationOptions,
            IServiceBroker serviceBroker,
            AuthorizationServiceClient? authorizationServiceClient)
        {
            // Dispose the AuthorizationServiceClient since we won't be using it
            authorizationServiceClient?.Dispose();

            var traceSource = (TraceSource?)hostProvidedServices.GetService(typeof(TraceSource));
            var brokeredServiceData = (RazorBrokeredServiceData?)hostProvidedServices.GetService(typeof(RazorBrokeredServiceData));

            var exportProvider = brokeredServiceData?.ExportProvider
                ?? await RemoteMefComposition.GetExportProviderAsync().ConfigureAwait(false);

            var targetLoggerFactory = brokeredServiceData?.LoggerFactory
                ?? (traceSource is not null
                    ? SimpleLoggerFactory.CreateWithTraceSource(traceSource)
                    : SimpleLoggerFactory.Empty);

            var remoteLoggerFactory = exportProvider.GetExportedValue<RemoteLoggerFactory>();
            remoteLoggerFactory.SetTargetLoggerFactory(targetLoggerFactory);

            var pipe = stream.UsePipe();

            var descriptor = RazorServices.Descriptors.GetDescriptorForServiceFactory(typeof(TService));
            var serverConnection = descriptor.WithTraceSource(traceSource).ConstructRpcConnection(pipe);

            var razorServiceBroker = new RazorServiceBroker(serviceBroker);
            var args = new ServiceArgs(razorServiceBroker, exportProvider);

            var service = CreateService(in args);

            serverConnection.AddLocalRpcTarget(service);
            serverConnection.StartListening();

            return service;
        }

        protected abstract TService CreateService(in ServiceArgs args);

        internal TestAccessor GetTestAccessor() => new(this);

        internal readonly struct TestAccessor(FactoryBase<TService> instance)
        {
            public TService CreateService(in ServiceArgs args)
                => instance.CreateService(in args);
        }
    }
}
