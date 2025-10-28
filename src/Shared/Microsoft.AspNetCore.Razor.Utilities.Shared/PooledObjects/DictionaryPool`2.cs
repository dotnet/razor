// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="Dictionary{TKey, TValue}"/> instances.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal sealed partial class DictionaryPool<TKey, TValue> : CustomObjectPool<Dictionary<TKey, TValue>>
    where TKey : notnull
{
    public static readonly DictionaryPool<TKey, TValue> Default = Create();

    private DictionaryPool(PooledObjectPolicy policy, int size)
        : base(policy, size)
    {
    }

    public static DictionaryPool<TKey, TValue> Create(
        IEqualityComparer<TKey> comparer, int size = DefaultPool.DefaultPoolSize)
        => new(new Policy(comparer), size);

    public static DictionaryPool<TKey, TValue> Create(
        PooledObjectPolicy policy, int size = DefaultPool.DefaultPoolSize)
        => new(policy, size);

    public static DictionaryPool<TKey, TValue> Create(int size = DefaultPool.DefaultPoolSize)
        => new(Policy.Instance, size);

    public static PooledObject<Dictionary<TKey, TValue>> GetPooledObject()
        => Default.GetPooledObject();

    public static PooledObject<Dictionary<TKey, TValue>> GetPooledObject(out Dictionary<TKey, TValue> map)
        => Default.GetPooledObject(out map);
}
