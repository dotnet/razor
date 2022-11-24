// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// Pooled <see cref="Dictionary{TKey, TValue}"/> instances when the key is of type <see cref="string"/>.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal static class StringDictionaryPool<TValue>
{
    public static class Ordinal
    {
        public static readonly ObjectPool<Dictionary<string, TValue>> DefaultPool =
            ObjectPool.Default(() => new Dictionary<string, TValue>(StringComparer.Ordinal));

        public static PooledObject<Dictionary<string, TValue>> GetPooledObject()
            => DefaultPool.GetPooledObject();

        public static PooledObject<Dictionary<string, TValue>> GetPooledObject(out Dictionary<string, TValue> map)
            => DefaultPool.GetPooledObject(out map);

    }

    public static class OrdinalIgnoreCase
    {
        public static readonly ObjectPool<Dictionary<string, TValue>> DefaultPool =
            ObjectPool.Default(() => new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase));

        public static PooledObject<Dictionary<string, TValue>> GetPooledObject()
            => DefaultPool.GetPooledObject();

        public static PooledObject<Dictionary<string, TValue>> GetPooledObject(out Dictionary<string, TValue> map)
            => DefaultPool.GetPooledObject(out map);
    }
}
