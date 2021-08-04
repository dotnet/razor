﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Feedback
{
    internal class FeedbackFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly FeedbackFileLogWriter _feedbackFileLogWriter;
        private readonly Scope _noopScope;

        public FeedbackFileLogger(string categoryName, FeedbackFileLogWriter feedbackFileLogWriter)
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
            _feedbackFileLogWriter = feedbackFileLogWriter;
            _noopScope = new Scope();
        }

        public IDisposable BeginScope<TState>(TState state) => _noopScope;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var formattedResult = formatter(state, exception);
            var logContent = $"[{_categoryName}] {formattedResult}";
            _feedbackFileLogWriter.Write(logContent);
        }

        private class Scope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
