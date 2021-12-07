// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer.JsonRpc
{
    public class TestLoggerFactory : ILoggerFactory
    {
        public static readonly TestLoggerFactory Instance = new();

        private TestLoggerFactory()
        {

        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new TestLogger();

        public void Dispose()
        {
        }

        private class TestLogger : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) => new DisposableScope();

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
            }

            private class DisposableScope : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }
    }
}
