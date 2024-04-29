// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.Razor.Logging;

internal abstract partial class AbstractLoggerFactory
{
    private class AggregateLogger(ImmutableArray<Lazy<ILogger>> lazyLoggers) : ILogger
    {
        private ImmutableArray<Lazy<ILogger>> _lazyLoggers = lazyLoggers;

        public bool IsEnabled(LogLevel logLevel)
        {
            foreach (var lazyLogger in _lazyLoggers)
            {
                if (lazyLogger.Value.IsEnabled(logLevel))
                {
                    return true;
                }
            }

            return false;
        }

        public void Log(LogLevel logLevel, string message, Exception? exception)
        {
            foreach (var lazyLogger in _lazyLoggers)
            {
                var logger = lazyLogger.Value;

                if (logger.IsEnabled(logLevel))
                {
                    logger.Log(logLevel, message, exception);
                }
            }
        }

        internal void AddLogger(Lazy<ILogger> lazyLogger)
        {
            ImmutableInterlocked.Update(ref _lazyLoggers, (set, l) => set.Add(l), lazyLogger);
        }
    }
}
