// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;
using Microsoft.VisualStudio.Razor.ServiceHub.Contracts;

namespace Microsoft.VisualStudio.Razor.ServiceHub
{
    public class RazorLanguageServerFactory : IServiceHubServiceFactory
    {
        // A class implementing IServiceHubServiceFactory needs to have a parameterless constructor
        public RazorLanguageServerFactory()
        {
        }

        public async Task<object> CreateAsync(Stream stream, IServiceProvider hostProvidedServices, ServiceActivationOptions serviceActivationOptions, IServiceBroker serviceBroker, AuthorizationServiceClient authorizationServiceClient)
        {
#if DEBUG
            System.Diagnostics.Debugger.Launch();

            while (true)
            {
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    break;
                }
            }

            System.Diagnostics.Debugger.Break();
#endif

            LanguageServerFeatureOptions vsLanguageServerFeatureOptions;
            HostServicesProvider vsHostWorkspaceServicesProvider;

            using (var vsOptionsService = await serviceBroker.GetProxyAsync<OptionsService>(ServiceHubDescriptors.VSOptionsService))
            {
                vsLanguageServerFeatureOptions = await vsOptionsService?.GetLanguageServerFeatureOptionsAsync();
                vsHostWorkspaceServicesProvider = await vsOptionsService?.GetHostWorkspaceServicesProviderAsync();
            }

            var trace = Trace.Verbose;
            var server = await RazorLanguageServer.CreateAsync(stream, stream, trace, ConfigureLanguageServer);
            await server.InitializedAsync(System.Threading.CancellationToken.None);

            return server;

            void ConfigureLanguageServer(RazorLanguageServerBuilder builder)
            {
                if (builder is null)
                {
                    throw new ArgumentNullException(nameof(builder));
                }

                var services = builder.Services;

                services.AddSingleton(vsLanguageServerFeatureOptions);
                services.AddSingleton(vsHostWorkspaceServicesProvider);
            }
        }
    }

    internal class OptionsService : IDisposable
    {
        public Task<LanguageServerFeatureOptions> GetLanguageServerFeatureOptionsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<HostServicesProvider> GetHostWorkspaceServicesProviderAsync()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
