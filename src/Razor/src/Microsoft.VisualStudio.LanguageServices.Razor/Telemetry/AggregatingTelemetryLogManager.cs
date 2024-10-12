// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.VisualStudio.Razor.Telemetry;

/// <summary>
/// Manages creation and obtaining aggregated telemetry logs.
/// </summary>
internal sealed class AggregatingTelemetryLogManager : IDisposable
{
    private readonly TelemetrySession _session;
    private ImmutableDictionary<string, AggregatingTelemetryLog> _aggregatingLogs = ImmutableDictionary<string, AggregatingTelemetryLog>.Empty;

    public AggregatingTelemetryLogManager(TelemetrySession session)
    {
        _session = session;
    }

    public AggregatingTelemetryLog? GetLog(string name, double[]? bucketBoundaries = null)
    {
        if (!_session.IsOptedIn)
            return null;

        return ImmutableInterlocked.GetOrAdd(
            ref _aggregatingLogs,
            name,
            static (functionId, arg) => new AggregatingTelemetryLog(arg._session, functionId, arg.bucketBoundaries),
            factoryArgument: (_session, bucketBoundaries));
    }

    public void Flush()
    {
        if (!_session.IsOptedIn)
            return;

        foreach (var log in _aggregatingLogs.Values)
        {
            log.Flush();
        }
    }

    public void Dispose()
    {
        Flush();
    }
}
