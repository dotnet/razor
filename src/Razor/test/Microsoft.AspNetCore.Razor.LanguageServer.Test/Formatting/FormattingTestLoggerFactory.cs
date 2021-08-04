// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Text;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class FormattingTestLoggerFactory : ILoggerFactory
    {
        private ITestOutputHelper _output;

        public FormattingTestLoggerFactory(ITestOutputHelper output)
        {
            _output = output;
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FormattingTestLogger(_output);
        }

        public void Dispose()
        {
        }
    }

    internal class FormattingTestLogger : ILogger
    {
        private readonly ITestOutputHelper _output;

        public FormattingTestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable BeginScope<TState>(TState state) =>
            new NullScope();

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var stringBuilder = new StringBuilder();
            var source = formatter(state, exception);
            stringBuilder.AppendLine(source);

            if (exception != null)
            {
                stringBuilder.AppendLine(exception.ToString());
            }

            try
            {
                _output.WriteLine(stringBuilder.ToString());
            }
            catch (Exception)
            {
            }
        }

        private class NullScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
