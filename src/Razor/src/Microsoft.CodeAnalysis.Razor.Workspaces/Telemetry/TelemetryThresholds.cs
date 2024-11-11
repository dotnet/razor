﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Telemetry;

/// <summary>
/// A set of constants used to reduce the telemetry emitted to the set that help us understand
/// which LSP is taking the most time in the case that the overall call is lengthy.
/// </summary>
internal static class TelemetryThresholds
{
    internal static readonly TimeSpan CodeActionRazorTelemetryThreshold = TimeSpan.FromMilliseconds(2000);
    internal static readonly TimeSpan CodeActionSubLSPTelemetryThreshold = TimeSpan.FromMilliseconds(1000);

    internal static readonly TimeSpan CompletionRazorTelemetryThreshold = TimeSpan.FromMilliseconds(4000);
    internal static readonly TimeSpan CompletionSubLSPTelemetryThreshold = TimeSpan.FromMilliseconds(2000);

    internal static readonly TimeSpan DiagnosticsRazorTelemetryThreshold = TimeSpan.FromMilliseconds(4000);
    internal static readonly TimeSpan DiagnosticsSubLSPTelemetryThreshold = TimeSpan.FromMilliseconds(2000);

    internal static readonly TimeSpan MapCodeRazorTelemetryThreshold = TimeSpan.FromMilliseconds(2000);
    internal static readonly TimeSpan MapCodeSubLSPTelemetryThreshold = TimeSpan.FromMilliseconds(1000);

    internal static readonly TimeSpan SemanticTokensRazorTelemetryThreshold = TimeSpan.FromMilliseconds(2000);
    internal static readonly TimeSpan SemanticTokensSubLSPTelemetryThreshold = TimeSpan.FromMilliseconds(1000);
}
