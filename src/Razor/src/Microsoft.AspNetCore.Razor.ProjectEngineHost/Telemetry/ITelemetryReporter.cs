﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Telemetry;

public interface ITelemetryReporter
{
    IDisposable BeginBlock(string name, Severity severity);
    IDisposable BeginBlock(string name, Severity severity, ImmutableDictionary<string, object?> values);
    void ReportEvent(string name, Severity severity);
    void ReportEvent(string name, Severity severity, ImmutableDictionary<string, object?> values);
    void ReportFault(Exception exception, string? message, params object?[] @params);
}
