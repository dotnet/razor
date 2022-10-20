// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.AspNetCore.Razor.Common.Telemetry;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Test.Common
{
    internal class NoOpTelemetryReporter : ITelemetryReporter
    {
        public static readonly NoOpTelemetryReporter Instance = new NoOpTelemetryReporter();
        public void ReportEvent(string name, TelemetrySeverity severity)
        {
        }

        public void ReportEvent<T>(string name, TelemetrySeverity severity, ImmutableDictionary<string, T> values)
        {
        }
    }
}
