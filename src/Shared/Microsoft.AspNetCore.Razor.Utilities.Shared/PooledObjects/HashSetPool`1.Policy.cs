// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial class HashSetPool<T>
{
    private sealed class Policy : PooledObjectPolicy
    {
        public static readonly Policy Default = new(comparer: null, DefaultPool.MaximumObjectSize);

        private readonly IEqualityComparer<T>? _comparer;
        private readonly int _maximumObjectSize;

        private Policy(IEqualityComparer<T>? comparer, int maximumObjectSize)
        {
            ArgHelper.ThrowIfNegative(maximumObjectSize);

            _comparer = comparer;
            _maximumObjectSize = maximumObjectSize;
        }

        public static Policy Create(
            Optional<IEqualityComparer<T>?> comparer = default,
            Optional<int> maximumObjectSize = default)
        {
            if ((!comparer.HasValue || comparer.Value == Default._comparer) &&
                (!maximumObjectSize.HasValue || maximumObjectSize.Value == Default._maximumObjectSize))
            {
                return Default;
            }

            return new(comparer.Value, maximumObjectSize.Value);
        }

        public override HashSet<T> Create() => new(_comparer);

        public override bool Return(HashSet<T> set)
        {
            var count = set.Count;

            set.Clear();

            if (count > _maximumObjectSize)
            {
                set.TrimExcess();
            }

            return true;
        }
    }
}
