// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.ServiceHub.Framework.Services;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.Razor.ServiceHub
{
    public class InteractiveServiceFactory : IServiceHubServiceFactory
    {
        // A class implementing IServiceHubServiceFactory needs to have a parameterless constructor
        public InteractiveServiceFactory()
        {
        }

        public Task<object> CreateAsync(Stream stream, IServiceProvider hostProvidedServices, ServiceActivationOptions serviceActivationOptions, IServiceBroker serviceBroker, AuthorizationServiceClient authorizationServiceClient)
        {
            CultureInfo.CurrentUICulture = serviceActivationOptions.ClientUICulture;
            var interactiveService = new InteractiveService();
            _ = JsonRpc.Attach(stream, interactiveService);

            return Task.FromResult<object>(interactiveService);
        }
    }
}
