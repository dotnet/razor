// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Language.Legacy;

internal abstract class KeywordSet
{
    public abstract int Count { get; }
    public abstract bool Contains(string keyword);

    public static KeywordSet Create(HashSet<string> set) => new HashSetBackedKeywordSet(set);

    public static KeywordSet Create(FrozenSet<string> set) => new FrozenSetBackedKeywordSet(set);

    private sealed class HashSetBackedKeywordSet(HashSet<string> set) : KeywordSet
    {
        public override int Count => set.Count;
        public override bool Contains(string keyword) => set.Contains(keyword);
    }

    private sealed class FrozenSetBackedKeywordSet(FrozenSet<string> set) : KeywordSet
    {
        public override int Count => set.Count;
        public override bool Contains(string keyword) => set.Contains(keyword);
    }
}
