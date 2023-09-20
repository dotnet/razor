// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using Microsoft.AspNetCore.Razor;

namespace Microsoft.VisualStudio.Editor.Razor.Snippets;

[Export(typeof(SnippetCache)), Shared]
internal class SnippetCache
{
    private ReadWriterLocker _lock = new();
    private ImmutableArray<SnippetInfo> _snippets;

    internal void Update(ImmutableArray<SnippetInfo> snippets)
    {
        using (_lock.EnterWriteLock())
        {
            _snippets = snippets;
        }
    }

    public ImmutableArray<SnippetInfo> GetSnippets()
    {
        using var _ = _lock.EnterReadLock();
        return _snippets;
    }
}
