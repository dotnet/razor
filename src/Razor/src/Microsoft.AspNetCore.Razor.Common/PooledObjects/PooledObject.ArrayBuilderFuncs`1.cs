// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal static partial class PooledObject
{
    private static class ArrayBuilderFuncs<T>
    {
        public static Func<ObjectPool<ImmutableArray<T>.Builder>, ImmutableArray<T>.Builder> Allocate = pool =>
        {
            var builder = pool.Allocate();
            builder.Clear();

            return builder;
        };

        public static Action<ObjectPool<ImmutableArray<T>.Builder>, ImmutableArray<T>.Builder> Release = (pool, builder) =>
        {
            if (builder is null)
            {
                return;
            }

            builder.Clear();

            if (builder.Capacity > Threshold)
            {
                builder.Capacity = Threshold;
            }

            pool.Free(builder);
        };
    }
}
