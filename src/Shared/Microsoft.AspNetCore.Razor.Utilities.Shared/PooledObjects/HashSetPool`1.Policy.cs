// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial class HashSetPool<T>
{
    private sealed class Policy(IEqualityComparer<T>? comparer) : PooledObjectPolicy
    {
        public static readonly Policy Default = new();

        private Policy()
            : this(comparer: null)
        {
        }

        public override HashSet<T> Create() => new(comparer);

        public override bool Return(HashSet<T> set)
        {
            var count = set.Count;

            set.Clear();

            if (count > DefaultPool.MaximumObjectSize)
            {
                set.TrimExcess();
            }

            return true;
        }
    }
}
