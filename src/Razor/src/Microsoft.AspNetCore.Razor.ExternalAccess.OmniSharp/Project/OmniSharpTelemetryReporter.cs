// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Telemetry;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp;

internal class OmniSharpTelemetryReporter : ITelemetryReporter
{
    public void ReportEvent(string name, Severity severity)
    {
    }

    public void ReportEvent<T>(string name, Severity severity, ImmutableDictionary<string, T> values)
    {
    }

    public void ReportFault(Exception exception, string? message, params object?[] @params)
    {
    }

    public IDisposable StartEventScope(string name, Severity severity)
    {
        return NullScope.Instance;
    }

    public IDisposable StartEventScope<T>(string name, Severity severity, ImmutableDictionary<string, T> values)
    {
        return NullScope.Instance;
    }

    private class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new NullScope();
        private NullScope() { }
        public void Dispose() { }
    }
}
