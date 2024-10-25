// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.Telemetry;

internal static class ITelemetryReporterExtensions
{
    // These extensions effectively make TimeSpan an optional parameter on BeginBlock
    internal static TelemetryScope BeginBlock(this ITelemetryReporter self, string name, Severity severity) { return self.BeginBlock(name, severity, TimeSpan.Zero); }
    internal static TelemetryScope BeginBlock(this ITelemetryReporter self, string name, Severity severity, Property property) { return self.BeginBlock(name, severity, TimeSpan.Zero, property); }
    internal static TelemetryScope BeginBlock(this ITelemetryReporter self, string name, Severity severity, Property property1, Property property2) { return self.BeginBlock(name, severity, TimeSpan.Zero, property1, property2); }
    internal static TelemetryScope BeginBlock(this ITelemetryReporter self, string name, Severity severity, Property property1, Property property2, Property property3) { return self.BeginBlock(name, severity, TimeSpan.Zero, property1, property2 , property3); }
    internal static TelemetryScope BeginBlock(this ITelemetryReporter self, string name, Severity severity, params ReadOnlySpan<Property> properties) { return self.BeginBlock(name, severity, TimeSpan.Zero, properties); }
}
