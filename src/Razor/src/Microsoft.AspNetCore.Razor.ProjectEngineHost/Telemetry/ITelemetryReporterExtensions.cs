﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.Telemetry;

internal static class ITelemetryReporterExtensions
{
    // These extensions effectively make TimeSpan an optional parameter on BeginBlock
    public static TelemetryScope BeginBlock(this ITelemetryReporter reporter, string name, Severity severity)
          => reporter.BeginBlock(name, severity, minTimeToReport: TimeSpan.Zero);

    public static TelemetryScope BeginBlock(this ITelemetryReporter reporter, string name, Severity severity, Property property)
        => reporter.BeginBlock(name, severity, minTimeToReport: TimeSpan.Zero, property);

    public static TelemetryScope BeginBlock(this ITelemetryReporter reporter, string name, Severity severity, Property property1, Property property2)
        => reporter.BeginBlock(name, severity, minTimeToReport: TimeSpan.Zero, property1, property2);

    public static TelemetryScope BeginBlock(this ITelemetryReporter reporter, string name, Severity severity, Property property1, Property property2, Property property3)
        => reporter.BeginBlock(name, severity, minTimeToReport: TimeSpan.Zero, property1, property2, property3);

    public static TelemetryScope BeginBlock(this ITelemetryReporter reporter, string name, Severity severity, params ReadOnlySpan<Property> properties)
        => reporter.BeginBlock(name, severity, minTimeToReport: TimeSpan.Zero, properties);
}
