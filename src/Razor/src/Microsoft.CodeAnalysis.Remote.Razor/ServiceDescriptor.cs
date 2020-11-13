// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.ServiceHub.Framework;
using StreamJsonRpc;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    /// <summary>
    /// Describes Razor remote brokered service. 
    /// Adds Razor and Roslyn specific formatters and RPC settings to the default implementation.
    /// </summary>
    internal sealed class ServiceDescriptor : ServiceJsonRpcDescriptor
    {
        private static readonly JsonRpcTargetOptions s_jsonRpcTargetOptions = new JsonRpcTargetOptions()
        {
            // Do not allow JSON-RPC to automatically subscribe to events and remote their calls.
            NotifyClientOfEvents = false,

            // Only allow public methods (may be on internal types) to be invoked remotely.
            AllowNonPublicInvocation = false
        };

        private ServiceDescriptor(ServiceMoniker serviceMoniker, Type? clientInterface)
            : base(serviceMoniker, clientInterface, Formatters.UTF8, MessageDelimiters.HttpLikeHeaders)
        {
        }

        private ServiceDescriptor(ServiceDescriptor copyFrom)
          : base(copyFrom)
        {
        }

        public static ServiceDescriptor CreateRemoteServiceDescriptor(string serviceName, Type? clientInterface)
            => new ServiceDescriptor(new ServiceMoniker(serviceName), clientInterface);

        protected override ServiceRpcDescriptor Clone()
            => new ServiceDescriptor(this);

        protected override IJsonRpcMessageFormatter CreateFormatter()
        {
            var formatter = base.CreateFormatter();
            var messageFormatter = (JsonMessageFormatter)formatter;
            messageFormatter.JsonSerializer.Converters.RegisterRazorConverters();
            return messageFormatter;
        }

        protected override JsonRpcConnection CreateConnection(JsonRpc jsonRpc)
        {
            jsonRpc.CancelLocallyInvokedMethodsWhenConnectionIsClosed = true;
            var connection = base.CreateConnection(jsonRpc);
            connection.LocalRpcTargetOptions = s_jsonRpcTargetOptions;
            return connection;
        }
    }
}
