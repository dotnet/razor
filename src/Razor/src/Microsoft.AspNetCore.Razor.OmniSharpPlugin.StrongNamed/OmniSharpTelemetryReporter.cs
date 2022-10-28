// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.AspNetCore.Razor.Common.Telemetry;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin.StrongNamed
{
    internal class OmniSharpTelemetryReporter : ITelemetryReporter
    {
        public void ReportEvent(string name, TelemetrySeverity severity)
        {
        }

        public void ReportEvent<T>(string name, TelemetrySeverity severity, ImmutableDictionary<string, T> values)
        {
        }
    }
}
