// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.CodeAnalysis.Remote.Razor.Logging;

internal partial class TraceSourceLoggerProvider(TraceSource traceSource) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(traceSource, categoryName);
    }
}
