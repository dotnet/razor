// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common.Telemetry
{
    internal static class TelemetryReporter
    {
        private static readonly object _guard = new();
        private static ImmutableArray<TelemetrySession> s_telemetrySessions = ImmutableArray<TelemetrySession>.Empty;

        public static void RegisterTelemetrySesssion(TelemetrySession session)
        {
            lock (_guard)
            {
                s_telemetrySessions = s_telemetrySessions.Add(session);
            }
        }

        public static void UnregisterTelemetrySesssion(TelemetrySession session)
        {
            lock (_guard)
            {
                s_telemetrySessions = s_telemetrySessions.Remove(session);
            }
        }

        public static void ReportEvent(string name, TelemetrySeverity severity)
        {
            var telemetryEvent = new TelemetryEvent(name, severity);
            Report(telemetryEvent);
        }

        private static void Report(TelemetryEvent telemetryEvent)
        {
            try
            {
                foreach (var session in s_telemetrySessions)
                {
                    session.PostEvent(telemetryEvent);
                }
            }
            catch (OutOfMemoryException)
            {
                // Do we want to failfast like Roslyn here?
            }
            catch
            {
                // No need to do anything here. We failed to report telemetry
                // which isn't good, but not catastrophic for a user
            }
        }
    }
}
