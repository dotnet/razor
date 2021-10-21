// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;

namespace Microsoft.VisualStudio.Razor.ServiceHub
{
    public class RazorLanguageServerFactory: IServiceHubServiceFactory
    {
        // A class implementing IServiceHubServiceFactory needs to have a parameterless constructor
        public RazorLanguageServerFactory()
        {
        }

        public async Task<object> CreateAsync(Stream stream, IServiceProvider hostProvidedServices, ServiceActivationOptions serviceActivationOptions, IServiceBroker serviceBroker, AuthorizationServiceClient authorizationServiceClient)
        {
            // _hostProvider = hostProvidedServices.;
            var trace = Trace.Verbose;
            var server = await RazorLanguageServer.CreateAsync(stream, stream, trace);
            await server.InitializedAsync(System.Threading.CancellationToken.None);

            return server;
        }
    }
}
