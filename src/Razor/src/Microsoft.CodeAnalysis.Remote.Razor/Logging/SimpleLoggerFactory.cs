// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.CodeAnalysis.Remote.Razor.Logging;

internal sealed class SimpleLoggerFactory : AbstractLoggerFactory
{
    public static ILoggerFactory Empty { get; } = new SimpleLoggerFactory([]);

    private SimpleLoggerFactory(ImmutableArray<ILoggerProvider> loggerProviders)
        : base(loggerProviders)
    {
    }

    public static ILoggerFactory CreateWithTraceSource(TraceSource traceSource)
        => new SimpleLoggerFactory([new TraceSourceLoggerProvider(traceSource)]);
}
