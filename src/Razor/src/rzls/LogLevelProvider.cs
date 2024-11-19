// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class LogLevelProvider(LogLevel logLevel)
{
    private int _logLevel = (int)logLevel;

    internal LogLevel GetLogLevel()
        => (LogLevel)_logLevel;

    internal void SetLogLevel(LogLevel logLevel)
        => Interlocked.Exchange(ref _logLevel, (int)logLevel);
}
