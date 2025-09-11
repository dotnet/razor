// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Composition;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost.Handlers;

namespace Microsoft.CodeAnalysis.Remote.Razor;

[Shared]
[Export(typeof(RoslynCompletionListCacheWrapper))]
internal class RoslynCompletionListCacheWrapper
{
    private CompletionListCacheWrapper? _cacheWrapper;

    public CompletionListCacheWrapper GetCache()
    {
        _cacheWrapper ??= new();
        return _cacheWrapper;
    }
}
