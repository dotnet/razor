﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.ServiceHub.Framework;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal abstract class RazorServiceBase : IDisposable
{
    protected readonly ServiceBrokerClient ServiceBrokerClient;

    public RazorServiceBase(IServiceBroker serviceBroker, ITelemetryReporter telemetryReporter)
    {
        RazorServices = new RazorServices(telemetryReporter);

#pragma warning disable VSTHRD012 // Provide JoinableTaskFactory where allowed
        ServiceBrokerClient = new ServiceBrokerClient(serviceBroker);
#pragma warning restore
    }

    protected RazorServices RazorServices { get; }

    public void Dispose()
    {
        ServiceBrokerClient.Dispose();
    }
}
