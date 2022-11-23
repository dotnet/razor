// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

internal static class ObjectPool
{
    private const int DefaultSize = 20;

    public static ObjectPool<T> Default<T>()
        where T : class, new()
        => DefaultPool<T>.Instance;

    public static ObjectPool<T> Default<T>(Func<T> factory)
        where T : class
        => new(factory, DefaultSize);

    private static class DefaultPool<T>
        where T : class, new()
    {
        public static readonly ObjectPool<T> Instance = new(() => new T(), DefaultSize);
    }
}
