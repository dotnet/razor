// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Test;
using Moq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public abstract class HandlerTestBase
    {
        public HandlerTestBase()
        {
            var logger = TestLogger.Instance;
            LoggerProvider = Mock.Of<HTMLCSharpLanguageServerLogHubLoggerProvider>(l =>
                l.CreateLogger(It.IsAny<string>()) == logger &&
                l.InitializeLoggerAsync(It.IsAny<CancellationToken>()) == Task.CompletedTask &&
                l.CreateLoggerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult<ILogger>(logger),
                MockBehavior.Strict);
        }

        internal HTMLCSharpLanguageServerLogHubLoggerProvider LoggerProvider { get; }
    }
}
