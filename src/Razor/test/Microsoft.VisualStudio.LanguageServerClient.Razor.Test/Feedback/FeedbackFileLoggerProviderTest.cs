﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Feedback
{
    public class FeedbackFileLoggerProviderTest
    {
        [Fact]
        public void CreateLogger_OmniSharpFrameworkCategory_DisabledByDefault()
        {
            // Arrange
            var fileLogWriter = Mock.Of<FeedbackFileLogWriter>(MockBehavior.Strict);
            var provider = new FeedbackFileLoggerProvider(fileLogWriter);
            var categoryName = FeedbackFileLoggerProvider.OmniSharpFrameworkCategoryPrefix + ".Test";

            // Act
            var logger = provider.CreateLogger(categoryName);

            // Assert
            Assert.False(logger.IsEnabled(LogLevel.Trace));
            provider.Dispose();
        }

        [Fact]
        public void CreateLogger_Category_EnabledByDefault()
        {
            // Arrange
            var fileLogWriter = Mock.Of<FeedbackFileLogWriter>(MockBehavior.Strict);
            var provider = new FeedbackFileLoggerProvider(fileLogWriter);
            var categoryName = "Test";

            // Act
            var logger = provider.CreateLogger(categoryName);

            // Assert
            Assert.True(logger.IsEnabled(LogLevel.Trace));
            provider.Dispose();
        }
    }
}
