// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Telemetry;

namespace Microsoft.VisualStudio.DevKit.Razor.Logging;

[Shared]
[Export(typeof(IDevKitTelemetryReporter))]
internal sealed class DevKitTelemetryReporter : IDevKitTelemetryReporter
{
    private TelemetrySession? _telemetrySession;
    private const string CollectorApiKey = "0c6ae279ed8443289764825290e4f9e2-1a736e7c-1324-4338-be46-fc2a58ae4d14-7255";
    private readonly ILogger? _logger;

    [ImportingConstructor]
    public DevKitTelemetryReporter([Import(AllowDefault = true)] ILoggerFactory? loggerFactory = null)
    {
        _logger = loggerFactory?.CreateLogger<DevKitTelemetryReporter>();
    }

    public void InitializeSession(string telemetryLevel, string? sessionId, bool isDefaultSession)
    {
        Debug.Assert(_telemetrySession == null);

        var sessionSettingsJson = CreateSessionSettingsJson(telemetryLevel, sessionId);
        var session = new TelemetrySession($"{{{sessionSettingsJson}}}");

        if (isDefaultSession)
        {
            TelemetryService.SetDefaultSession(session);
        }

        session.Start();
        session.RegisterForReliabilityEvent();

        _telemetrySession = session;
    }

    public IDisposable BeginBlock(string name, Severity severity)
    {
        return BeginBlock(name, severity, ImmutableDictionary<string, object?>.Empty);
    }

    public IDisposable BeginBlock(string name, Severity severity, ImmutableDictionary<string, object?> values)
    {
        return new TelemetryScope(this, name, severity, values.ToImmutableDictionary((tuple) => tuple.Key, (tuple) => (object?)tuple.Value));
    }

    public void ReportEvent(string name, Severity severity)
    {
        var telemetryEvent = new TelemetryEvent(GetTelemetryName(name), ToTelemetrySeverity(severity));
        Report(telemetryEvent);
    }

    public void ReportEvent(string name, Severity severity, ImmutableDictionary<string, object?> values)
    {
        var telemetryEvent = new TelemetryEvent(GetTelemetryName(name), ToTelemetrySeverity(severity));
        foreach (var (propertyName, propertyValue) in values)
        {
            if (IsNumeric(propertyValue))
            {
                telemetryEvent.Properties.Add(GetPropertyName(propertyName), propertyValue);
            }
            else
            {
                telemetryEvent.Properties.Add(GetPropertyName(propertyName), new TelemetryComplexProperty(propertyValue));
            }
        }

        Report(telemetryEvent);
    }

    public void ReportFault(Exception exception, string? message, params object?[] @params)
    {
        try
        {
            if (exception is OperationCanceledException { InnerException: { } oceInnerException })
            {
                ReportFault(oceInnerException, message, @params);
                return;
            }

            if (exception is AggregateException aggregateException)
            {
                // We (potentially) have multiple exceptions; let's just report each of them
                foreach (var innerException in aggregateException.Flatten().InnerExceptions)
                {
                    ReportFault(innerException, message, @params);
                }

                return;
            }

            var currentProcess = Process.GetCurrentProcess();

            var faultEvent = new FaultEvent(
                eventName: GetTelemetryName("fault"),
                description: GetDescription(exception),
                FaultSeverity.General,
                exceptionObject: exception,
                gatherEventDetails: faultUtility =>
                {
                    foreach (var data in @params)
                    {
                        if (data is null)
                        {
                            continue;
                        }

                        faultUtility.AddErrorInformation(data.ToString());
                    }

                    // Returning "0" signals that, if sampled, we should send data to Watson.
                    // Any other value will cancel the Watson report. We never want to trigger a process dump manually,
                    // we'll let TargetedNotifications determine if a dump should be collected.
                    // See https://aka.ms/roslynnfwdocs for more details
                    return 0;
                });

            Report(faultEvent);
        }
        catch (Exception)
        {
        }
    }

    private static string GetTelemetryName(string name) => "dotnet/razor/" + name;
    private static string GetPropertyName(string name) => "dotnet.razor." + name;

    public IDisposable TrackLspRequest(string lspMethodName, string languageServerName, Guid correlationId)
    {
        if (correlationId == Guid.Empty)
        {
            return NullTelemetryScope.Instance;
        }

        return BeginBlock("TrackLspRequest", Severity.Normal, ImmutableDictionary.CreateRange(new KeyValuePair<string, object?>[]
        {
            new("eventscope.method", lspMethodName),
            new("eventscope.languageservername", languageServerName),
            new("eventscope.correlationid", correlationId),
        }));
    }

    private static string CreateSessionSettingsJson(string telemetryLevel, string? sessionId)
    {
        sessionId ??= Guid.NewGuid().ToString();

        // Generate a new startTime for process to be consumed by Telemetry Settings
        using var curProcess = Process.GetCurrentProcess();
        var processStartTime = curProcess.StartTime.ToFileTimeUtc().ToString();

        var sb = new StringBuilder();

        var kvp = new Dictionary<string, string>
        {
            { "Id", StringToJsonValue(sessionId) },
            { "HostName", StringToJsonValue("Default") },

            // Insert Telemetry Level instead of Opt-Out status. The telemetry service handles
            // validation of this value so there is no need to do so on this end. If it's invalid,
            // it defaults to off.
            { "TelemetryLevel", StringToJsonValue(telemetryLevel) },

            // this sets the Telemetry Session Created by LSP Server to be the Root Initial session
            // This means that the SessionID set here by "Id" will be the SessionID used by cloned session
            // further down stream
            { "IsInitialSession", "true" },
            { "CollectorApiKey", StringToJsonValue(CollectorApiKey) },

            // using 1010 to indicate VS Code and not to match it to devenv 1000
            { "AppId", "1010" },
            { "ProcessStartTime", processStartTime },
        };

        foreach (var keyValue in kvp)
        {
            sb.AppendFormat("\"{0}\":{1},", keyValue.Key, keyValue.Value);
        }

        return sb.ToString().TrimEnd(',');

        static string StringToJsonValue(string? value)
        {
            if (value == null)
            {
                return "null";
            }

            return '"' + value + '"';
        }
    }

    private static TelemetrySeverity ToTelemetrySeverity(Severity severity)
    => severity switch
    {
        Severity.Normal => TelemetrySeverity.Normal,
        Severity.Low => TelemetrySeverity.Low,
        Severity.High => TelemetrySeverity.High,
        _ => throw new InvalidOperationException($"Unknown severity: {severity}")
    };

    private void Report(TelemetryEvent telemetryEvent)
    {
        try
        {
#if !DEBUG
            foreach (var session in _telemetrySessions)
            {
                session.PostEvent(telemetryEvent);
            }
#else
            // In debug we only log to normal logging. This makes it much easier to add and debug telemetry events
            // before we're ready to send them to the cloud
            var name = telemetryEvent.Name;
            var propertyString = string.Join(",", telemetryEvent.Properties.Select(kvp => $"[ {kvp.Key}:{kvp.Value} ]"));
            _logger?.LogTrace("Telemetry Event: {name} \n Properties: {propertyString}\n", name, propertyString);

            if (telemetryEvent is FaultEvent)
            {
                var eventType = telemetryEvent.GetType();
                var description = eventType.GetProperty("Description", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(telemetryEvent, null);
                var exception = eventType.GetProperty("ExceptionObject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(telemetryEvent, null);
                var message = $"Fault Event: {name} \n Exception Info: {exception ?? description} \n Properties: {propertyString}";

                Debug.Assert(true, message);
            }
#endif
        }
        catch (Exception e)
        {
            // No need to do anything here. We failed to report telemetry
            // which isn't good, but not catastrophic for a user
            _logger?.LogError(e, "Failed logging telemetry event");
        }
    }

    private static string GetDescription(Exception exception)
    {
        const string CodeAnalysisNamespace = nameof(Microsoft) + "." + nameof(CodeAnalysis);
        const string AspNetCoreNamespace = nameof(Microsoft) + "." + nameof(AspNetCore);

        // Be resilient to failing here.  If we can't get a suitable name, just fall back to the standard name we
        // used to report.
        try
        {
            // walk up the stack looking for the first call from a type that isn't in the ErrorReporting namespace.
            var frames = new StackTrace(exception).GetFrames();

            // On the .NET Framework, GetFrames() can return null even though it's not documented as such.
            // At least one case here is if the exception's stack trace itself is null.
            if (frames != null)
            {
                foreach (var frame in frames)
                {
                    var method = frame?.GetMethod();
                    var methodName = method?.Name;
                    if (methodName is null)
                    {
                        continue;
                    }

                    var declaringTypeName = method?.DeclaringType?.FullName;
                    if (declaringTypeName == null)
                    {
                        continue;
                    }

                    if (!declaringTypeName.StartsWith(CodeAnalysisNamespace) &&
                        !declaringTypeName.StartsWith(AspNetCoreNamespace))
                    {
                        continue;
                    }

                    return declaringTypeName + "." + methodName;
                }
            }
        }
        catch
        {
        }

        // If we couldn't get a stack, do this
        return exception.Message;
    }

    private static bool IsNumeric(object? o)
        => o is not null &&
        !o.GetType().IsEnum &&
        Type.GetTypeCode(o.GetType()) switch
        {
            TypeCode.Char or
            TypeCode.SByte or
            TypeCode.Byte or
            TypeCode.Int16 or
            TypeCode.Int32 or
            TypeCode.Int64 or
            TypeCode.Double or
            TypeCode.Single or
            TypeCode.UInt16 or
            TypeCode.UInt32 or
            TypeCode.UInt64
            => true,
            _ => false
        };

    private class NullTelemetryScope : IDisposable
    {
        public static NullTelemetryScope Instance { get; } = new NullTelemetryScope();
        private NullTelemetryScope() { }
        public void Dispose() { }
    }

    private class TelemetryScope : IDisposable
    {
        private readonly ITelemetryReporter _telemetryReporter;
        private string _name;
        private Severity _severity;
        private ImmutableDictionary<string, object?> _values;
        private bool _disposed;
        private Stopwatch _stopwatch;

        public TelemetryScope(ITelemetryReporter telemetryReporter, string name, Severity severity, ImmutableDictionary<string, object?> values)
        {
            _telemetryReporter = telemetryReporter;
            _name = name;
            _severity = severity;
            _values = values;
            _stopwatch = StopwatchPool.Default.Get();
            _stopwatch.Restart();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _stopwatch.Stop();
            var values = _values.Add("eventscope.ellapsedms", _stopwatch.ElapsedMilliseconds);
            _telemetryReporter.ReportEvent(_name, _severity, values);
            StopwatchPool.Default.Return(_stopwatch);
        }
    }
}
