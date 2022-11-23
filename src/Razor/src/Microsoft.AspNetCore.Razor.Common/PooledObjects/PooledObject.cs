// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal static partial class PooledObject
{
    internal const int Threshold = 512;

    public static PooledObject<ImmutableArray<T>.Builder> GetPooledObject<T>(this ObjectPool<ImmutableArray<T>.Builder> pool)
        => new(pool, ArrayBuilderFuncs<T>.Allocate, ArrayBuilderFuncs<T>.Release);

    public static PooledObject<Dictionary<TKey, TValue>> GetPooledObject<TKey, TValue>(this ObjectPool<Dictionary<TKey, TValue>> pool)
        where TKey : notnull
        => new(pool, DictionaryFuncs<TKey, TValue>.Allocate, DictionaryFuncs<TKey, TValue>.Release);

    public static PooledObject<HashSet<T>> GetPooledObject<T>(this ObjectPool<HashSet<T>> pool)
        => new(pool, HashSetFuncs<T>.Allocate, HashSetFuncs<T>.Release);

    public static PooledObject<List<T>> GetPooledObject<T>(this ObjectPool<List<T>> pool)
        => new(pool, ListFuncs<T>.Allocate, ListFuncs<T>.Release);

    public static PooledObject<Stack<T>> GetPooledObject<T>(this ObjectPool<Stack<T>> pool)
        => new(pool, StackFuncs<T>.Allocate, StackFuncs<T>.Release);

    public static PooledObject<Stopwatch> GetPooledObject(this ObjectPool<Stopwatch> pool)
        => new(pool, StopwatchFuncs.Allocate, StopwatchFuncs.Release);

    public static PooledObject<StringBuilder> GetPooledObject(this ObjectPool<StringBuilder> pool)
        => new(pool, StringBuilderFuncs.Allocate, StringBuilderFuncs.Release);
}
