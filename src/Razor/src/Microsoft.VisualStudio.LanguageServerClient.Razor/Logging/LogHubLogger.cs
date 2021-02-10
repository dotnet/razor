// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Logging
{
    internal class LogHubLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly LogHubLogWriter _logWriter;
        private readonly Scope _noopScope;

        public LogHubLogger(string categoryName, LogHubLogWriter feedbackFileLogWriter)
        {
            if (categoryName is null)
            {
                throw new ArgumentNullException(nameof(categoryName));
            }

            if (feedbackFileLogWriter is null)
            {
                throw new ArgumentNullException(nameof(feedbackFileLogWriter));
            }

            _categoryName = categoryName;
            _logWriter = feedbackFileLogWriter;
            _noopScope = new Scope();
        }

        public IDisposable BeginScope<TState>(TState state) => _noopScope;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var formattedResult = formatter(state, exception);
            _logWriter.Write("[{0}] {1}", _categoryName, formattedResult);
        }

        private class Scope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
