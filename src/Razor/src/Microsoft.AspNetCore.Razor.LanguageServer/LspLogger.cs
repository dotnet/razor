// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

/// <summary>
/// ILogger implementation that logs via the window/logMessage LSP method
/// </summary>
internal class LspLogger : ILogger
{
    private readonly LogLevel _logLevel;
    private readonly string _categoryName;
    private IClientConnection _clientConnection;

    public LspLogger(string categoryName, Trace trace, IClientConnection clientConnection)
    {
        var logLevel = trace switch
        {
            Trace.Off => LogLevel.None,
            Trace.Messages => LogLevel.Information,
            Trace.Verbose => LogLevel.Trace,
            _ => throw new NotImplementedException(),
        };
        _logLevel = logLevel;
        _categoryName = categoryName;
        _clientConnection = clientConnection;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return new Disposable();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel is LogLevel.None)
        {
            return;
        }

        var messageType = logLevel switch
        {
            LogLevel.Critical => MessageType.Error,
            LogLevel.Error => MessageType.Error,
            LogLevel.Warning => MessageType.Warning,
            LogLevel.Information => MessageType.Info,
            LogLevel.Debug => MessageType.Log,
            LogLevel.Trace => MessageType.Log,
            _ => throw new NotImplementedException(),
        };
        var message = formatter(state, exception);
        if (message == "[null]" && exception is not null)
        {
            message = exception.ToString();
        }

        var @params = new LogMessageParams
        {
            MessageType = messageType,
            Message = $"[{_categoryName}] {message}",
        };

        if (_clientConnection is null)
        {
            throw new InvalidOperationException($"Tried to log before {nameof(IClientConnection)} was set.");
        }

        _ = _clientConnection.SendNotificationAsync(Methods.WindowLogMessageName, @params, CancellationToken.None);
    }

    private class Disposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
