// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

public class LoggerWrapper : ILspLogger, ILogger
{
    private readonly ILogger _logger;

    public LoggerWrapper(ILogger logger)
    {
        _logger = logger;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return _logger.BeginScope<TState>(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _logger.IsEnabled(logLevel);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _logger.Log<TState>(logLevel, eventId, state, exception, formatter);
    }

    public void LogEndContext(string message, params object[] @params)
    {
        _logger.LogInformation("Exiting: {}", message);
    }

    public void LogError(string message, params object[] @params)
    {
#pragma warning disable CA2254 // Template should be a static expression
        _logger.LogError(message, @params);
    }

    public void LogException(Exception exception, string? message = null, params object[] @params)
    {
        _logger.LogError(exception, message, @params);
    }

    public void LogInformation(string message, params object[] @params)
    {
        _logger.LogInformation(message, @params);
    }

    public void LogStartContext(string message, params object[] @params)
    {
        _logger.LogInformation("Entering: {}", message);
    }

    public void LogWarning(string message, params object[] @params)
    {
        _logger.LogWarning(message, @params);
#pragma warning restore CA2254 // Template should be a static expression
    }
}
