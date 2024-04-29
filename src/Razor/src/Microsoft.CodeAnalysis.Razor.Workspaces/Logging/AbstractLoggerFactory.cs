// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;

#if !NET7_0_OR_GREATER
using System.Collections.Generic;
#endif

namespace Microsoft.CodeAnalysis.Razor.Logging;

internal abstract partial class AbstractLoggerFactory : ILoggerFactory
{
    private ImmutableArray<Lazy<ILoggerProvider>> _providers;
    private ImmutableDictionary<string, AggregateLogger> _loggers;

    protected AbstractLoggerFactory(ImmutableArray<Lazy<ILoggerProvider>> providers)
    {
        _providers = providers;
        _loggers = ImmutableDictionary.Create<string, AggregateLogger>(StringComparer.OrdinalIgnoreCase);
    }

    public ILogger GetOrCreateLogger(string categoryName)
    {
        if (_loggers.TryGetValue(categoryName, out var logger))
        {
            return logger;
        }

        using var lazyLoggers = new PooledArrayBuilder<Lazy<ILogger>>(_providers.Length);

        foreach (var provider in _providers)
        {
            lazyLoggers.Add(new(() => provider.Value.CreateLogger(categoryName)));
        }

        var result = new AggregateLogger(lazyLoggers.DrainToImmutable());
        return ImmutableInterlocked.AddOrUpdate(ref _loggers, categoryName, result, (k, v) => v);
    }

    public void AddLoggerProvider(ILoggerProvider provider)
    {
        var lazyProvider = new Lazy<ILoggerProvider>(() => provider);

        if (ImmutableInterlocked.Update(ref _providers, (set, p) => set.Add(p), lazyProvider))
        {
            foreach (var (category, logger) in _loggers)
            {
                logger.AddLogger(new(() => provider.CreateLogger(category)));
            }
        }
    }
}
