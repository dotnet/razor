// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.Test.Common
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

        public ILogger CreateLogger(string categoryName) => new TestLspLogger();

        public void Dispose()
        {
        }
    }
}
