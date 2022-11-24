// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="ImmutableArray{T}.Builder"/> instances.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal static class ArrayBuilderPool<T>
{
    public static readonly ObjectPool<ImmutableArray<T>.Builder> DefaultPool = ObjectPool.Default(ImmutableArray.CreateBuilder<T>);

    public static PooledObject<ImmutableArray<T>.Builder> GetPooledObject()
        => DefaultPool.GetPooledObject();

    public static PooledObject<ImmutableArray<T>.Builder> GetPooledObject(out ImmutableArray<T>.Builder builder)
        => DefaultPool.GetPooledObject(out builder);
}
