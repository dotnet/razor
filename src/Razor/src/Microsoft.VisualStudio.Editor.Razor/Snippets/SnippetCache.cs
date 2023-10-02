// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;

namespace Microsoft.VisualStudio.Editor.Razor.Snippets;

[Export(typeof(SnippetCache)), Shared]
internal class SnippetCache
{
    private ImmutableArray<SnippetInfo> _snippets;

    internal void Update(ImmutableArray<SnippetInfo> snippets)
        => ImmutableInterlocked.InterlockedExchange(ref _snippets, snippets);

    public ImmutableArray<SnippetInfo> GetSnippets() => _snippets;
}
