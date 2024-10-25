// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Telemetry;

/// <summary>
/// A set of constants used to reduce the telemetry emitted to the set that help us understand
/// which LSP is taking the most time in the case that the overall call is lengthy.
/// </summary>
internal class TelemetryThresholds
{
    internal const int CodeActionRazorTelemetryThresholdMS = 2000;
    internal const int CodeActionSubLSPTelemetryThresholdMS = 1000;

    internal const int CompletionRazorTelemetryThresholdMS = 4000;
    internal const int CompletionSubLSPTelemetryThresholdMS = 2000;

    internal const int DiagnosticsRazorTelemetryThresholdMS = 4000;
    internal const int DiagnosticsSubLSPTelemetryThresholdMS = 2000;

    internal const int MapCodeRazorTelemetryThresholdMS = 2000;
    internal const int MapCodeSubLSPTelemetryThresholdMS = 1000;

    internal const int SemanticTokensRazorTelemetryThresholdMS = 2000;
    internal const int SemanticTokensSubLSPTelemetryThresholdMS = 1000;
}
