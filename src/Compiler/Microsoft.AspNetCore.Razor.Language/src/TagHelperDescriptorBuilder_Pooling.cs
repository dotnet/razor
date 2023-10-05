﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

public partial class TagHelperDescriptorBuilder
{
    private static readonly ObjectPool<TagHelperDescriptorBuilder> s_pool = DefaultPool.Create(Policy.Instance);

    private static readonly ObjectPool<HashSet<AllowedChildTagDescriptor>> s_allowedChildTagSetPool
        = HashSetPool<AllowedChildTagDescriptor>.Create(AllowedChildTagDescriptorComparer.Default);

    private static readonly ObjectPool<HashSet<BoundAttributeDescriptor>> s_boundAttributeSetPool
        = HashSetPool<BoundAttributeDescriptor>.Create(BoundAttributeDescriptorComparer.Default);

    private static readonly ObjectPool<HashSet<TagMatchingRuleDescriptor>> s_tagMatchingRuleSetPool
        = HashSetPool<TagMatchingRuleDescriptor>.Create(TagMatchingRuleDescriptorComparer.Default);

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

    private protected override void Reset()
    {
        _kind = null;
        _name = null;
        _assemblyName = null;
        _documentationObject = default;

        DisplayName = null;
        TagOutputHint = null;
        CaseSensitive = false;

        AllowedChildTags.Clear();
        BoundAttributes.Clear();
        TagMatchingRules.Clear();

        _metadata.Clear();
    }

    private sealed class Policy : PooledBuilderPolicy<TagHelperDescriptorBuilder>
    {
        public static readonly Policy Instance = new();

        private Policy()
        {
        }

        public override TagHelperDescriptorBuilder Create() => new();
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
