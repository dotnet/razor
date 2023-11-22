// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.VisualStudio.Editor.Razor.Logging;

public class TestOutputWindowLogger : IOutputWindowLogger
{
    public static TestOutputWindowLogger Instance { get; } = new();

    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return false;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
    }

    public void SetTestLogger(ILogger testOutputLogger)
    {
    }
}
