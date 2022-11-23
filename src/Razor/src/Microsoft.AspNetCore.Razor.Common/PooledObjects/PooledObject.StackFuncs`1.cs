// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal static partial class PooledObject
{
    private static class StackFuncs<T>
    {
        public static Func<ObjectPool<Stack<T>>, Stack<T>> Allocate = pool =>
        {
            var stack = pool.Allocate();
            stack.Clear();

            return stack;
        };

        public static Action<ObjectPool<Stack<T>>, Stack<T>> Release = (pool, stack) =>
        {
            if (stack is null)
            {
                return;
            }

            var count = stack.Count;
            stack.Clear();

            if (count > Threshold)
            {
                stack.TrimExcess();
            }

            pool.Free(stack);
        };
    }
}
