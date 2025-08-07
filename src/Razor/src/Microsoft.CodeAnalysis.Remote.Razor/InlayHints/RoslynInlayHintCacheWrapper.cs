// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost.Handlers;
using Microsoft.CodeAnalysis.Razor.Remote;

namespace Microsoft.CodeAnalysis.Remote.Razor;

[Shared]
[Export(typeof(ILspLifetimeService))]
[Export(typeof(RoslynInlayHintCacheWrapper))]
internal class RoslynInlayHintCacheWrapper : ILspLifetimeService
{
    private InlayHintCacheWrapper? _inlayHintCacheWrapper;

    public InlayHintCacheWrapper GetCache()
    {
        _inlayHintCacheWrapper ??= new();
        return _inlayHintCacheWrapper;
    }

    void ILspLifetimeService.OnLspInitialized(RemoteClientLSPInitializationOptions options)
    {
    }

    void ILspLifetimeService.OnLspUninitialized()
    {
        _inlayHintCacheWrapper = null;
    }
}
