// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.ServiceHub.Client;
using Microsoft.ServiceHub.Framework;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    /// <summary>
    /// Service descriptors of brokered Roslyn ServiceHub services.
    /// </summary>
    internal static class ServiceDescriptors
    {
        /// <summary>
        /// Brokered services must be defined in Microsoft.VisualStudio service namespace in order to be considered first party.
        /// </summary>
        internal const string ServiceNamePrefix = "Microsoft.VisualStudio.Razor.";

        private const string InterfaceNamePrefix = "IRemote";
        private const string InterfaceNameSuffix = "Service";

        internal static readonly ImmutableDictionary<Type, ServiceDescriptor> Descriptors = ImmutableDictionary.CreateRange(new[]
        {
            CreateDescriptor(typeof(IRemoteLanguageService), callbackInterface: null),
        });

        //private static string GetServiceName(Type serviceInterface) => "Microsoft.VisualStudio.Razor.RemoteLanguageService";

        internal static string GetServiceName(Type serviceInterface)
        {
            var interfaceName = serviceInterface.Name;
            return interfaceName.Substring(InterfaceNamePrefix.Length, interfaceName.Length - InterfaceNamePrefix.Length - InterfaceNameSuffix.Length);
        }

        private static KeyValuePair<Type, ServiceDescriptor> CreateDescriptor(Type serviceInterface, Type? callbackInterface = null)
        {
            Debug.Assert(callbackInterface == null || callbackInterface.IsInterface);

            var serviceName = GetServiceName(serviceInterface);
            var descriptor = ServiceDescriptor.CreateRemoteServiceDescriptor(serviceName, callbackInterface);
            return new KeyValuePair<Type, ServiceDescriptor>(serviceInterface, descriptor);
        }

        public static ServiceRpcDescriptor GetServiceDescriptor(Type serviceType)
            => Descriptors[serviceType];
    }
}
