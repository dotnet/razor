﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.AspNetCore.Razor.Test.Common.Logging;

internal partial class TestOutputLogger : ILogger
{
    [ThreadStatic]
    private static StringBuilder? g_builder;

    private readonly TestOutputLoggerProvider _provider;

    public string? CategoryName { get; }
    public LogLevel LogLevel { get; }

    public TestOutputLogger(
        TestOutputLoggerProvider provider,
        string? categoryName = null,
        LogLevel logLevel = LogLevel.Trace)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        CategoryName = categoryName;
        LogLevel = logLevel;
    }

    public bool IsEnabled(LogLevel logLevel)
        => logLevel >= LogLevel;

    public void Log(LogLevel logLevel, string message, Exception? exception)
    {
        if (!IsEnabled(logLevel) || _provider.TestOutputHelper is null)
        {
            return;
        }

        var builder = GetEmptyBuilder();

        var time = DateTime.Now.TimeOfDay;
        var leadingTimeStamp = $"[{time:hh\\:mm\\:ss\\.fffffff}] ";
        var leadingSpaces = new string(' ', leadingTimeStamp.Length);
        var lines = message.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

        var isFirstLine = true;

        builder.Append(leadingTimeStamp);

        if (CategoryName is { } categoryName)
        {
            builder.Append($"[{categoryName}] ");
            isFirstLine = lines.Length == 1;
        }

        foreach (var line in lines)
        {
            if (!isFirstLine)
            {
                builder.AppendLine();
                builder.Append(leadingSpaces);
            }

            builder.Append(line);

            isFirstLine = false;
        }

        var finalMessage = builder.ToString();

        try
        {
            _provider.TestOutputHelper.WriteLine(finalMessage);
        }
        catch (InvalidOperationException iex) when (iex.Message == "There is no currently active test.")
        {
            // Ignore, something is logging a message outside of a test. Other loggers will capture it.
        }
        catch (Exception ex)
        {
            // If an exception is thrown while writing a message, throw an AggregateException that includes
            // the message that was being logged, along with the exception that was thrown and any exception
            // that was being logged. This might provide clues to the cause.

            var innerExceptions = new List<Exception>
            {
                ex
            };

            // Were we logging an exception? If so, add that too.
            if (exception is not null)
            {
                innerExceptions.Add(exception);
            }

            var aggregateException = new AggregateException($"An exception occurred while logging: {finalMessage}", innerExceptions);
            throw aggregateException.Flatten();
        }
    }

    private static StringBuilder GetEmptyBuilder()
    {
        if (g_builder is null)
        {
            g_builder = new();
        }
        else
        {
            g_builder.Clear();
        }

        return g_builder;
    }
}
