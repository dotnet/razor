// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Common.Telemetry;
using Microsoft.ServiceHub.Framework;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    internal sealed class RemoteTagHelperProviderServiceFactory : RazorServiceFactoryBase<IRemoteTagHelperProviderService>
    {
        private readonly ITelemetryReporter _telemetryReporter;

        public RemoteTagHelperProviderServiceFactory(ITelemetryReporter telemetryReporter) : base(RazorServiceDescriptors.TagHelperProviderServiceDescriptors)
        {
            _telemetryReporter = telemetryReporter;
        }

        protected override IRemoteTagHelperProviderService CreateService(IServiceBroker serviceBroker)
                => new RemoteTagHelperProviderService(serviceBroker, _telemetryReporter);
    }
}
