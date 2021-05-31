// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class TestLoggerFactory : ILoggerFactory
    {
        private ITestOutputHelper _output;

        public TestLoggerFactory(ITestOutputHelper output)
        {
            _output = output;
        }

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XunitLogger(_output, categoryName, LogLevel.Information, DateTimeOffset.Now); ;
        }

        public void Dispose()
        {
        }
    }
}
