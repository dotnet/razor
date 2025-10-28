// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.AspNetCore.Razor.PooledObjects;

/// <summary>
/// A pool of <see cref="StringBuilder"/> instances.
/// </summary>
/// 
/// <remarks>
/// Instances originating from this pool are intended to be short-lived and are suitable
/// for temporary work. Do not return them as the results of methods or store them in fields.
/// </remarks>
internal sealed partial class StringBuilderPool : CustomObjectPool<StringBuilder>
{
    public static readonly StringBuilderPool Default = Create();

    private StringBuilderPool(PooledObjectPolicy policy, int size)
        : base(policy, size)
    {
    }

    public static StringBuilderPool Create(
        PooledObjectPolicy policy, int size = DefaultPool.DefaultPoolSize)
        => new(policy, size);

    public static StringBuilderPool Create(int size = DefaultPool.DefaultPoolSize)
        => new(Policy.Default, size);

    public static PooledObject<StringBuilder> GetPooledObject()
        => Default.GetPooledObject();

    public static PooledObject<StringBuilder> GetPooledObject(out StringBuilder builder)
        => Default.GetPooledObject(out builder);
}
