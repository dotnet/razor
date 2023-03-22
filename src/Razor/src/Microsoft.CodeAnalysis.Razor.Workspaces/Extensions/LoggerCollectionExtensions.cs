// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
internal static class LoggerCollectionExtensions
{

    public static void LogTrace(this IEnumerable<ILogger> loggers, string? message, params object?[] args)
        => Log(loggers, LogLevel.Trace, message, args);

    public static void LogDebug(this IEnumerable<ILogger> loggers, string? message, params object?[] args)
        => Log(loggers, LogLevel.Debug, message, args);

    public static void LogInformation(this IEnumerable<ILogger> loggers, string? message, params object?[] args)
        => Log(loggers, LogLevel.Information, message, args);

    public static void LogWarning(this IEnumerable<ILogger> loggers, string? message, params object?[] args)
        => Log(loggers, LogLevel.Warning, message, args);

    public static void LogError(this IEnumerable<ILogger> loggers, Exception? e, string? message, params object?[] args)
        => Log(loggers, LogLevel.Error, e, message, args);

    public static void LogCritical(this IEnumerable<ILogger> loggers, string? message, params object?[] args)
        => Log(loggers, LogLevel.Critical, message, args);

    private static void Log(this IEnumerable<ILogger> loggers, LogLevel logLevel, Exception? exception, string? message, object?[] args)
    {
        foreach (var logger in loggers)
        {
            logger.Log(logLevel, exception, message, args);
        }
    }

    private static void Log(this IEnumerable<ILogger> loggers, LogLevel logLevel, string? message, object?[] args)
    {
        foreach (var logger in loggers)
        {
            logger.Log(logLevel, message, args);
        }
    }
}
