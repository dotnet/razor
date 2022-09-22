// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Telemetry
{
    internal class TelemetryReporter : ITelemetryReporter
    {
        private readonly ImmutableArray<TelemetrySession> _telemetrySessions;
        private readonly ILogger _logger;

        public TelemetryReporter(ImmutableArray<TelemetrySession> telemetrySessions, ILoggerFactory loggerFactory)
        {
            _telemetrySessions = telemetrySessions;
            _logger = loggerFactory.CreateLogger<TelemetryReporter>();
        }

        public void ReportEvent(string name, TelemetrySeverity severity)
        {
            var telemetryEvent = new TelemetryEvent(name, severity);
            Report(telemetryEvent);
        }

        private void Report(TelemetryEvent telemetryEvent)
        {
            try
            {
                foreach (var session in _telemetrySessions)
                {
                    session.PostEvent(telemetryEvent);
                }
            }
            catch (OutOfMemoryException)
            {
                // Do we want to failfast like Roslyn here?
            }
            catch (Exception e)
            {
                // No need to do anything here. We failed to report telemetry
                // which isn't good, but not catastrophic for a user
                _logger.LogError(e, "Failed logging telemetry event");
            }
        }
    }
}
