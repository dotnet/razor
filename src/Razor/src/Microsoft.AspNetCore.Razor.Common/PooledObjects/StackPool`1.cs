// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="Stack{T}"/> instances.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal static class StackPool<T>
{
    internal const int Threshold = 512;

    private static readonly Func<ObjectPool<Stack<T>>, Stack<T>> s_allocate = AllocateAndClear;
    private static readonly Action<ObjectPool<Stack<T>>, Stack<T>> s_release = ClearAndFree;

    public static ObjectPool<Stack<T>> DefaultPool { get; } = ObjectPool.Default<Stack<T>>();

    public static PooledObject<Stack<T>> GetPooledObject()
        => new(DefaultPool, s_allocate, s_release);

    private static Stack<T> AllocateAndClear(ObjectPool<Stack<T>> pool)
    {
        var stack = pool.Allocate();
        stack.Clear();

        return stack;
    }

    private static void ClearAndFree(ObjectPool<Stack<T>> pool, Stack<T> stack)
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
    }
}
