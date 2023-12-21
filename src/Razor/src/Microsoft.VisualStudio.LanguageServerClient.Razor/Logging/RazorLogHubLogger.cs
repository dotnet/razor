// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.Extensions.Logging;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;

internal sealed class RazorLogHubLogger : ILogger
{
    private string _categoryName;
    private TraceSource _traceSource;

    public RazorLogHubLogger(string categoryName, TraceSource traceSource)
    {
        _categoryName = categoryName;
        _traceSource = traceSource;
    }

    public IDisposable BeginScope<TState>(TState state) => Scope.Instance;

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var formattedResult = formatter(state, exception);

        switch (logLevel)
        {
            // We separate out Information because we want to check for specific log messages set from CLaSP
            case LogLevel.Information:
                // The category for start and stop will only ever be "CLaSP" so no point logging it
                if (formattedResult.StartsWith(ClaspLoggingBridge.LogStartContextMarker))
                {
                    _traceSource.TraceEvent(TraceEventType.Start, id: 0, "{0}", formattedResult);
                }
                else if (formattedResult.StartsWith(ClaspLoggingBridge.LogEndContextMarker))
                {
                    _traceSource.TraceEvent(TraceEventType.Stop, id: 0, "{0}", formattedResult);
                }
                else
                {
                    _traceSource.TraceEvent(TraceEventType.Information, id: 0, "[{0}] {1}", _categoryName, formattedResult);
                }

                break;

            case LogLevel.Trace:
            case LogLevel.Debug:
            case LogLevel.None:
                _traceSource.TraceEvent(TraceEventType.Information, id: 0, "[{0}] {1}", _categoryName, formattedResult);
                break;
            case LogLevel.Warning:
                _traceSource.TraceEvent(TraceEventType.Warning, id: 0, "[{0}] {1}", _categoryName, formattedResult);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                _traceSource.TraceEvent(TraceEventType.Error, id: 0, "[{0}] {1} {2}", _categoryName, formattedResult, exception!);
                break;
        }
    }

    private class Scope : IDisposable
    {
        public static readonly Scope Instance = new();

        public void Dispose()
        {
        }
    }
}
