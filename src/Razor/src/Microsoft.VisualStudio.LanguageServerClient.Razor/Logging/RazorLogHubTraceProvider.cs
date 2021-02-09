// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.LogHub;
using VSShell = Microsoft.VisualStudio.Shell;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Logging
{
    [Shared]
    [Export(typeof(RazorLogHubTraceProvider))]
    internal class RazorLogHubTraceProvider
    {
        private static readonly LoggerOptions _logOptions = new LoggerOptions(
            privacySetting: PrivacyFlags.CanContainPersonallyIdentifibleInformation | PrivacyFlags.CanContainPrivateInformation,
            systemTags: new string[] { "Debug", "Build" });

        private readonly SemaphoreSlim _initializationSemaphore = null;
        private IServiceBroker _serviceBroker = null;

        public RazorLogHubTraceProvider()
        {
            _initializationSemaphore = new SemaphoreSlim(1, 1);
        }

        public async Task<TraceSource> InitializeTraceAsync(string logIdentifier, int logHubSessionId)
        {
            if (!await TryInitializeServiceBrokerAsync().ConfigureAwait(false))
            {
                return null;
            }

            var _logId = new LogId(
                logName: $"{logIdentifier}.{logHubSessionId}",
                serviceId: new ServiceMoniker($"Razor.{logIdentifier}"));

            using var traceConfig = await TraceConfiguration.CreateTraceConfigurationInstanceAsync(_serviceBroker).ConfigureAwait(false);
            var traceSource = await traceConfig.RegisterLogSourceAsync(_logId, _logOptions).ConfigureAwait(false);
            traceSource.Switch.Level = SourceLevels.ActivityTracing | SourceLevels.Information;

            return traceSource;
        }

        public async Task<bool> TryInitializeServiceBrokerAsync()
        {
            await _initializationSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                // Check if the service broker has already been initialized
                if (!(_serviceBroker is null))
                {
                    return true;
                }

                if (!(VSShell.Package.GetGlobalService(typeof(VSShell.Interop.SAsyncServiceProvider)) is VSShell.IAsyncServiceProvider serviceProvider))
                {
                    return false;
                }

                var serviceContainer = await VSShell.ServiceExtensions.GetServiceAsync<
                    VSShell.ServiceBroker.SVsBrokeredServiceContainer,
                    VSShell.ServiceBroker.IBrokeredServiceContainer>(serviceProvider).ConfigureAwait(false);
                if (serviceContainer is null)
                {
                    return false;
                }

                _serviceBroker = serviceContainer.GetFullAccessServiceBroker();
                if (_serviceBroker is null)
                {
                    return false;
                }

                return true;
            }
            finally
            {
                _initializationSemaphore.Release();
            }
        }
    }
}
