// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

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
internal static class DictionaryPool<TKey, TValue>
    where TKey : notnull
{
    public static readonly ObjectPool<Dictionary<TKey, TValue>> DefaultPool = ObjectPool.Default<Dictionary<TKey, TValue>>();

    public static PooledObject<Dictionary<TKey, TValue>> GetPooledObject()
        => DefaultPool.GetPooledObject();

    public static PooledObject<Dictionary<TKey, TValue>> GetPooledObject(out Dictionary<TKey, TValue> map)
        => DefaultPool.GetPooledObject(out map);
}
