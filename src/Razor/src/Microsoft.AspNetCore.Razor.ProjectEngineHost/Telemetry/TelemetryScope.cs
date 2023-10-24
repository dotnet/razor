// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Telemetry;

internal sealed class TelemetryScope : IDisposable
{
    public static readonly TelemetryScope Null = new();

    private readonly ITelemetryReporter? _reporter;
    private readonly string _name;
    private readonly Severity _severity;
    private readonly ImmutableArray<Property>.Builder _properties;
    private readonly Stopwatch _stopwatch;
    private bool _disposed;

    private TelemetryScope()
    {
        _reporter = null;
        _name = null!;
        _properties = null!;
        _stopwatch = null!;
    }

    private TelemetryScope(
        ITelemetryReporter reporter,
        string name,
        Severity severity,
        ImmutableArray<Property>.Builder properties)
    {
        _reporter = reporter;
        _name = name;
        _severity = severity;

        // Note: The builder that is passed in always has its capacity set to at least
        // 1 larger than the number of properties to allow the final "ellapsedms"
        // property to be added in Dispose below.
        _properties = properties;

        _stopwatch = StopwatchPool.Default.Get();
        _stopwatch.Restart();
    }

    public void Dispose()
    {
        if (_reporter is null || _disposed)
        {
            return;
        }

        _disposed = true;

        _stopwatch.Stop();
        _properties.Add(new("eventscope.ellapsedms", _stopwatch.ElapsedMilliseconds));

        _reporter.ReportEvent(
            _name,
            _severity,
            _properties.DrainToImmutable());

        ArrayBuilderPool<Property>.Default.Return(_properties);
        StopwatchPool.Default.Return(_stopwatch);
    }

    public static TelemetryScope Create(ITelemetryReporter reporter, string name, Severity severity)
    {
        var builder = ArrayBuilderPool<Property>.Default.Get();
        builder.SetCapacityIfLarger(1);

        return new(reporter, name, severity, builder);
    }

    public static TelemetryScope Create(ITelemetryReporter reporter, string name, Severity severity, Property property)
    {
        var builder = ArrayBuilderPool<Property>.Default.Get();
        builder.SetCapacityIfLarger(2);
        builder.Add(property);

        return new(reporter, name, severity, builder);
    }

    public static TelemetryScope Create(ITelemetryReporter reporter, string name, Severity severity, Property property1, Property property2)
    {
        var builder = ArrayBuilderPool<Property>.Default.Get();
        builder.SetCapacityIfLarger(3);
        builder.Add(property1);
        builder.Add(property2);

        return new(reporter, name, severity, builder);
    }

    public static TelemetryScope Create(ITelemetryReporter reporter, string name, Severity severity, Property property1, Property property2, Property property3)
    {
        var builder = ArrayBuilderPool<Property>.Default.Get();
        builder.SetCapacityIfLarger(4);
        builder.Add(property1);
        builder.Add(property2);
        builder.Add(property3);

        return new(reporter, name, severity, builder);
    }

    public static TelemetryScope Create(ITelemetryReporter reporter, string name, Severity severity, ImmutableArray<Property> properties)
    {
        var builder = ArrayBuilderPool<Property>.Default.Get();
        builder.SetCapacityIfLarger(properties.Length + 1);
        builder.AddRange(properties);

        return new(reporter, name, severity, builder);
    }

    public static TelemetryScope Create(ITelemetryReporter reporter, string name, Severity severity, Property[] properties)
    {
        var builder = ArrayBuilderPool<Property>.Default.Get();
        builder.SetCapacityIfLarger(properties.Length + 1);
        builder.AddRange(properties);

        return new(reporter, name, severity, builder);
    }
}
