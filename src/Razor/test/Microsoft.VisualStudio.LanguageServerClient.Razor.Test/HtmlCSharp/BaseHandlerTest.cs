// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;
using Moq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public abstract class BaseHandlerTest
    {
        public BaseHandlerTest()
        {
            var logger = new Mock<ILogger>(MockBehavior.Strict).Object;
            Mock.Get(logger).Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>())).Verifiable();
            LoggerProvider = Mock.Of<HTMLCSharpLanguageServerLogHubLoggerProvider>(l => l.CreateLogger(It.IsAny<string>()) == logger, MockBehavior.Strict);
        }

        internal HTMLCSharpLanguageServerLogHubLoggerProvider LoggerProvider { get; }
    }
}
