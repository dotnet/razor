// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial class ArrayBuilderPool<T>
{
    private sealed class Policy : PooledObjectPolicy
    {
        public static readonly Policy Default = new(DefaultMaximumObjectSize);

        private readonly int _maximumObjectSize;

        private Policy(int maximumObjectSize)
        {
            _maximumObjectSize = maximumObjectSize;
        }

        public static Policy Create(Optional<int> maximumObjectSize = default)
        {
            if (!maximumObjectSize.HasValue || maximumObjectSize.Value == Default._maximumObjectSize)
            {
                return Default;
            }

            return new(maximumObjectSize.Value);
        }

        public override ImmutableArray<T>.Builder Create()
            => ImmutableArray.CreateBuilder<T>();

        public override bool Return(ImmutableArray<T>.Builder builder)
        {
            var count = builder.Count;

            builder.Clear();

            if (count > _maximumObjectSize)
            {
                builder.Capacity = _maximumObjectSize;
            }

            return true;
        }
    }
}
