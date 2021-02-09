// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Logging
{
    [Shared]
    [Export(typeof(HTMLCSharpLanguageServerLogHubLoggerProvider))]
    internal class HTMLCSharpLanguageServerLogHubLoggerProvider : ILoggerProvider
    {
        private static readonly string LogFileIdentifier = "HTMLCSharpLanguageServer";

        private LogHubLoggerProvider _loggerProvider;

        public readonly Task InitializationTask;

        // Internal for testing
        internal HTMLCSharpLanguageServerLogHubLoggerProvider()
        {
        }

        [ImportingConstructor]
        public HTMLCSharpLanguageServerLogHubLoggerProvider(
            HTMLCSharpLanguageServerLogHubLoggerProviderFactory loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            InitializationTask = Task.Run(async () =>
            {
                _loggerProvider = (LogHubLoggerProvider)await loggerFactory.GetOrCreateAsync(LogFileIdentifier).ConfigureAwait(false);
            });
        }

        // Virtual for testing
        public virtual ILogger CreateLogger(string categoryName) =>
            _loggerProvider.CreateLogger(categoryName);

        public TraceSource GetTraceSource() =>
            _loggerProvider.GetTraceSource();

        public void Dispose()
        {
        }
    }
}
