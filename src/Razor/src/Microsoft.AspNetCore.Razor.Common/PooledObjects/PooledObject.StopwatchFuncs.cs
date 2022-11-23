// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal static partial class PooledObject
{
    private static class StopwatchFuncs
    {
        public static Func<ObjectPool<Stopwatch>, Stopwatch> Allocate = pool =>
        {
            var watch = pool.Allocate();
            watch.Reset();

            return watch;
        };

        public static Action<ObjectPool<Stopwatch>, Stopwatch> Release = (pool, watch) =>
        {
            if (watch is null)
            {
                return;
            }

            watch.Reset();
            pool.Free(watch);
        };
    }
}
