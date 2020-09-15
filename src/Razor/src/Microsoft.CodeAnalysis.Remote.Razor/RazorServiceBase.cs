// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;
using Nerdbank.Streams;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    internal abstract class RazorServiceBase : IDisposable
    {
        internal abstract class FactoryBase<TService> : IServiceHubServiceFactory
            where TService : class
        {
            public Task<object> CreateAsync(
               Stream stream,
               IServiceProvider hostProvidedServices,
               ServiceActivationOptions serviceActivationOptions,
               IServiceBroker serviceBroker,
               AuthorizationServiceClient? authorizationServiceClient)
            {
                // Dispose the AuthorizationServiceClient since we won't be using it
                authorizationServiceClient?.Dispose();

                return Task.FromResult((object)Create(stream.UsePipe(), serviceBroker));
            }

            internal TService Create(IDuplexPipe pipe, IServiceBroker serviceBroker)
            {
                var descriptor = ServiceDescriptors.GetServiceDescriptor(typeof(TService));
                var serverConnection = descriptor.ConstructRpcConnection(pipe);

                var service = CreateService(serviceBroker);

                serverConnection.AddLocalRpcTarget(service);
                serverConnection.StartListening();

                return service;
            }

            protected abstract TService CreateService(IServiceBroker serviceBroker);
        }

        protected readonly ServiceBrokerClient ServiceBrokerClient;

        public RazorServiceBase(IServiceBroker serviceBroker)
        {
            RazorServices = new RazorServices();

#pragma warning disable VSTHRD012 // Provide JoinableTaskFactory where allowed
            ServiceBrokerClient = new ServiceBrokerClient(serviceBroker);
#pragma warning restore
        }

        public void Dispose()
        {
            ServiceBrokerClient.Dispose();
        }

        protected RazorServices RazorServices { get; }

        protected virtual Task<ProjectSnapshot> GetProjectSnapshotAsync(ProjectSnapshotHandle projectHandle, CancellationToken cancellationToken)
        {
            if (projectHandle == null)
            {
                throw new ArgumentNullException(nameof(projectHandle));
            }

            return Task.FromResult<ProjectSnapshot>(new SerializedProjectSnapshot(projectHandle.FilePath, projectHandle.Configuration, projectHandle.RootNamespace));
        }

        private static IEnumerable<JsonConverter> GetRazorConverters()
        {
            var collection = new JsonConverterCollection();
            collection.RegisterRazorConverters();
            return collection;
        }

        private class SerializedProjectSnapshot : ProjectSnapshot
        {
            public SerializedProjectSnapshot(string filePath, RazorConfiguration configuration, string rootNamespace)
            {
                FilePath = filePath;
                Configuration = configuration;
                RootNamespace = rootNamespace;

                Version = VersionStamp.Default;
            }

            public override RazorConfiguration Configuration { get; }

            public override IEnumerable<string> DocumentFilePaths => Array.Empty<string>();

            public override string FilePath { get; }

            public override string RootNamespace { get; }

            public override VersionStamp Version { get; }

            public override DocumentSnapshot? GetDocument(string filePath)
            {
                if (filePath == null)
                {
                    throw new ArgumentNullException(nameof(filePath));
                }

                return null;
            }

            public override bool IsImportDocument(DocumentSnapshot document)
            {
                throw new NotImplementedException();
            }

            public override IEnumerable<DocumentSnapshot> GetRelatedDocuments(DocumentSnapshot document)
            {
                throw new NotImplementedException();
            }

            public override RazorProjectEngine GetProjectEngine()
            {
                throw new NotImplementedException();
            }
        }
    }
}
