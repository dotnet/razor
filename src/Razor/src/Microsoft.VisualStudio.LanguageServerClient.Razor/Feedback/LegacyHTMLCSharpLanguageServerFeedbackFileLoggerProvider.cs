// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Composition;
using Microsoft.Extensions.Logging;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Feedback
{
    [Shared]
    [Export(typeof(LegacyHTMLCSharpLanguageServerFeedbackFileLoggerProvider))]
    [Obsolete("Use the LogHub logging infrastructure instead.")]
    internal class LegacyHTMLCSharpLanguageServerFeedbackFileLoggerProvider : ILoggerProvider
    {
        private const string LogFileIdentifier = "HTMLCSharpLanguageServer";

        private readonly FeedbackFileLoggerProvider _loggerProvider;

        // Internal for testing
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        [Obsolete("For testing only")]
        internal LegacyHTMLCSharpLanguageServerFeedbackFileLoggerProvider()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        [ImportingConstructor]
        public LegacyHTMLCSharpLanguageServerFeedbackFileLoggerProvider(
            HTMLCSharpLanguageServerFeedbackFileLoggerProviderFactory loggerFactory)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _loggerProvider = (FeedbackFileLoggerProvider)loggerFactory.GetOrCreate(LogFileIdentifier);
        }

        // Virtual for testing
        public virtual ILogger CreateLogger(string categoryName) => _loggerProvider.CreateLogger(categoryName);

        public void Dispose()
        {
        }
    }
}
