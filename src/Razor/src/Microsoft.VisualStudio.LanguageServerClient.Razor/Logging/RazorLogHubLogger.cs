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
    private RazorLogHubLoggerProvider _razorLogHubLoggerProvider;

    public RazorLogHubLogger(string categoryName, RazorLogHubLoggerProvider razorLogHubLoggerProvider)
    {
        _categoryName = categoryName;
        _razorLogHubLoggerProvider = razorLogHubLoggerProvider;
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
                if (formattedResult.StartsWith(ClaspLoggingBridge.LogStartContextMarker))
                {
                    _razorLogHubLoggerProvider.Queue(TraceEventType.Start, "[{0}] {1}", _categoryName, formattedResult);
                }

                _razorLogHubLoggerProvider.Queue(TraceEventType.Information, "[{0}] {1}", _categoryName, formattedResult);

                if (formattedResult.StartsWith(ClaspLoggingBridge.LogEndContextMarker))
                {
                    _razorLogHubLoggerProvider.Queue(TraceEventType.Stop, "[{0}] {1}", _categoryName, formattedResult);
                }

                break;

            case LogLevel.Trace:
            case LogLevel.Debug:
            case LogLevel.None:
                _razorLogHubLoggerProvider.Queue(TraceEventType.Information, "[{0}] {1}", _categoryName, formattedResult);
                break;
            case LogLevel.Warning:
                _razorLogHubLoggerProvider.Queue(TraceEventType.Warning, "[{0}] {1}", _categoryName, formattedResult);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                _razorLogHubLoggerProvider.Queue(TraceEventType.Error, "[{0}] {1} {2}", _categoryName, formattedResult, exception!);
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
