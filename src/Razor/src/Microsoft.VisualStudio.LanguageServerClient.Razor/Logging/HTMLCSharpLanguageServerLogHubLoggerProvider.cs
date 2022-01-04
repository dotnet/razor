// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Feedback;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Logging
{
    [Shared]
    [Export(typeof(HTMLCSharpLanguageServerLogHubLoggerProvider))]
    internal class HTMLCSharpLanguageServerLogHubLoggerProvider : ILoggerProvider
    {
        private const string LogFileIdentifier = "Razor.HTMLCSharpLanguageServerClient";

        private LogHubLoggerProvider? _loggerProvider;

        private LogHubLoggerProvider LoggerProvider
        {
            get
            {
                if (_loggerProvider is null)
                {
                    throw new InvalidOperationException($"{nameof(LoggerProvider)} accessed before being set.");
                }

                return _loggerProvider;
            }
        }

        private readonly HTMLCSharpLanguageServerLogHubLoggerProviderFactory _loggerFactory;
        private readonly SemaphoreSlim _initializationSemaphore;

        // Internal for testing
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        internal HTMLCSharpLanguageServerLogHubLoggerProvider()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        [ImportingConstructor]
        public HTMLCSharpLanguageServerLogHubLoggerProvider(
            HTMLCSharpLanguageServerLogHubLoggerProviderFactory loggerFactory,
#pragma warning disable CS0618 // Type or member is obsolete
            // We're purposely using the legacy feedback file logger here to create a marker
            // file. This marker file is used to identify bug reports using the new experimental
            // Razor editor.
            LegacyHTMLCSharpLanguageServerFeedbackFileLoggerProvider feedbackLoggerProvider)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (feedbackLoggerProvider is null)
            {
                throw new ArgumentNullException(nameof(feedbackLoggerProvider));
            }

            _loggerFactory = loggerFactory;

            _initializationSemaphore = new SemaphoreSlim(initialCount: 1, maxCount: 1);

            CreateMarkerFeedbackLoggerFile(feedbackLoggerProvider);
        }

        // Virtual for testing
        public virtual async Task InitializeLoggerAsync(CancellationToken cancellationToken)
        {
            if (_loggerProvider is not null)
            {
                return;
            }

            await _initializationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (_loggerProvider is null)
                {
                    _loggerProvider = (LogHubLoggerProvider)await _loggerFactory.GetOrCreateAsync(LogFileIdentifier, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _initializationSemaphore.Release();
            }
        }

        // Virtual for testing
        public virtual ILogger CreateLogger(string categoryName)
        {
            return LoggerProvider.CreateLogger(categoryName);
        }

        public virtual async Task<ILogger> CreateLoggerAsync(string categoryName, CancellationToken cancellationToken)
        {
            await InitializeLoggerAsync(cancellationToken);
            return CreateLogger(categoryName);
        }

        public TraceSource GetTraceSource()
        {
            return LoggerProvider.GetTraceSource();
        }

        public void Dispose()
        {
        }

        // We instantiate and create a basic log message through the Feedback logging system to ensure we still create
        // a RazorLogs*.zip file. This zip file is used to quickly identify whether or not we're using the new LSP powered Razor.
#pragma warning disable CS0618 // Type or member is obsolete
        private static void CreateMarkerFeedbackLoggerFile(LegacyHTMLCSharpLanguageServerFeedbackFileLoggerProvider feedbackLoggerProvider)
        {
            var feedbackLogger = feedbackLoggerProvider.CreateLogger(nameof(LegacyHTMLCSharpLanguageServerFeedbackFileLoggerProvider));
#pragma warning restore CS0618 // Type or member is obsolete
            feedbackLogger.LogInformation("Please take a look at the LogHub zip file for the full set of Razor logs.");
        }
    }
}
