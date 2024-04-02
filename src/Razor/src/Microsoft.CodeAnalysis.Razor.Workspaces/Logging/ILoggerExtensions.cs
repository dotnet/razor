// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.Razor.Logging;

internal static class ILoggerExtensions
{
    public static void LogTrace(this ILogger logger, string? message, params object?[] args)
    {
        Log(logger, LogLevel.Trace, exception: null, message, args);
    }

    public static void LogDebug(this ILogger logger, string? message, params object?[] args)
    {
        Log(logger, LogLevel.Debug, exception: null, message, args);
    }

    public static void LogInformation(this ILogger logger, string? message, params object?[] args)
    {
        Log(logger, LogLevel.Information, exception: null, message, args);
    }

    public static void LogWarning(this ILogger logger, string? message, params object?[] args)
    {
        Log(logger, LogLevel.Warning, exception: null, message, args);
    }

    public static void LogWarning(this ILogger logger, Exception exception, string? message, params object?[] args)
    {
        Log(logger, LogLevel.Warning, exception, message, args);
    }

    public static void LogError(this ILogger logger, string? message, params object?[] args)
    {
        Log(logger, LogLevel.Error, exception: null, message, args);
    }

    public static void LogError(this ILogger logger, Exception exception, string? message, params object?[] args)
    {
        Log(logger, LogLevel.Error, exception, message, args);
    }

    public static void LogCritical(this ILogger logger, Exception exception, string? message, params object?[] args)
    {
        Log(logger, LogLevel.Critical, exception, message, args);
    }

    private static void Log(ILogger logger, LogLevel level, Exception? exception, string? message, object?[] args)
    {
        logger.Log(level, (message, args), exception, FormatMessage);
    }

    private static string FormatMessage((string? message, object?[] args) s, Exception? exception)
    {
        return s.message is null ? "[null]" : string.Format(s.message, s.args);
    }
}
