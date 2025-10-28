// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal partial class ArrayBuilderPool<T>
{
    private sealed class Policy : PooledObjectPolicy
    {
        public static readonly Policy Default = new();

        private Policy()
        {
        }

        public override ImmutableArray<T>.Builder Create()
            => ImmutableArray.CreateBuilder<T>();

        public override bool Return(ImmutableArray<T>.Builder builder)
        {
            var count = builder.Count;

            builder.Clear();

            if (count > DefaultPool.MaximumObjectSize)
            {
                builder.Capacity = DefaultPool.MaximumObjectSize;
            }

            return true;
        }
    }
}
