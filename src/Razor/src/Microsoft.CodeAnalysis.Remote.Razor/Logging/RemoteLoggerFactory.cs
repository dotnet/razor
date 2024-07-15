// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.CodeAnalysis.Remote.Razor.Logging;

[Shared]
[Export(typeof(ILoggerFactory))]
[Export(typeof(RemoteLoggerFactory))]
internal sealed class RemoteLoggerFactory : ILoggerFactory
{
    private ILoggerFactory? _targetLoggerFactory;

    private ILoggerFactory TargetLoggerFactory => _targetLoggerFactory.AssumeNotNull();

    internal void SetTargetLoggerFactory(ILoggerFactory loggerFactory)
    {
        if (_targetLoggerFactory is not null)
        {
            throw new InvalidOperationException($"{nameof(_targetLoggerFactory)} is already set.");
        }

        _targetLoggerFactory = loggerFactory;
    }

    public void AddLoggerProvider(ILoggerProvider provider)
    {
        TargetLoggerFactory.AddLoggerProvider(provider);
    }

    public ILogger GetOrCreateLogger(string categoryName)
    {
        return TargetLoggerFactory.GetOrCreateLogger(categoryName);
    }

    public void Dispose()
    {
        // Don't dispose the inner ILoggerFactory because we don't own it.
    }
}
