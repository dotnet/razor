// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Text;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal static partial class PooledObject
{
    private static class StringBuilderFuncs
    {
        public static Func<ObjectPool<StringBuilder>, StringBuilder> Allocate = pool =>
        {
            var builder = pool.Allocate();
            builder.Clear();

            return builder;
        };

        public static Action<ObjectPool<StringBuilder>, StringBuilder> Release = (pool, builder) =>
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
