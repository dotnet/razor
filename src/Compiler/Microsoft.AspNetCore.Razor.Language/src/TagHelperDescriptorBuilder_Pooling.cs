// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

public partial class TagHelperDescriptorBuilder
{
    private static readonly ObjectPool<TagHelperDescriptorBuilder> s_pool = DefaultPool.Create(Policy.Instance);

    internal static TagHelperDescriptorBuilder GetInstance(string name, string assemblyName)
        => GetInstance(TagHelperConventions.DefaultKind, name, assemblyName);

    internal static TagHelperDescriptorBuilder GetInstance(string kind, string name, string assemblyName)
    {
        var builder = s_pool.Get();

        builder._kind = kind ?? throw new ArgumentNullException(nameof(kind));
        builder._name = name ?? throw new ArgumentNullException(nameof(name));
        builder._assemblyName = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));

        return builder;
    }

    internal static void ReturnInstance(TagHelperDescriptorBuilder builder)
        => s_pool.Return(builder);

    /// <summary>
    ///  Retrieves a pooled <see cref="TagHelperDescriptorBuilder"/> instance.
    /// </summary>
    /// <remarks>
    ///  The <see cref="PooledBuilder"/> returned by this method should be disposed
    ///  to return the <see cref="TagHelperDescriptorBuilder"/> to its pool.
    ///  The correct way to achieve this is with a using statement:
    ///
    /// <code>
    ///  using var _ = TagHelperDescriptorBuilder.GetPooledInstance(..., out var builder);
    /// </code>
    /// 
    ///  Once disposed, the builder can no longer be used.
    /// </remarks>
    public static PooledBuilder GetPooledInstance(
        string kind, string name, string assemblyName,
        out TagHelperDescriptorBuilder builder)
    {
        var defaultBuilder = GetInstance(kind, name, assemblyName);
        builder = defaultBuilder;
        return new(defaultBuilder);
    }

    /// <summary>
    ///  Retrieves a pooled <see cref="TagHelperDescriptorBuilder"/> instance.
    /// </summary>
    /// <remarks>
    ///  The <see cref="PooledBuilder"/> returned by this method should be disposed
    ///  to return the <see cref="TagHelperDescriptorBuilder"/> to its pool.
    ///  The correct way to achieve this is with a using statement:
    ///
    /// <code>
    ///  using var _ = TagHelperDescriptorBuilder.GetPooledInstance(..., out var builder);
    /// </code>
    /// 
    ///  Once disposed, the builder can no longer be used.
    /// </remarks>
    public static PooledBuilder GetPooledInstance(
        string name, string assemblyName,
        out TagHelperDescriptorBuilder builder)
    {
        var defaultBuilder = GetInstance(name, assemblyName);
        builder = defaultBuilder;
        return new(defaultBuilder);
    }
}
