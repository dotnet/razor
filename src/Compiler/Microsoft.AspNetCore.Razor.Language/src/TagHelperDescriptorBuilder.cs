// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed partial class TagHelperDescriptorBuilder
{
    private static readonly ObjectPool<HashSet<AllowedChildTagDescriptor>> s_allowedChildTagSetPool
        = HashSetPool<AllowedChildTagDescriptor>.Create(AllowedChildTagDescriptorComparer.Default);

    private static readonly ObjectPool<HashSet<BoundAttributeDescriptor>> s_boundAttributeSetPool
        = HashSetPool<BoundAttributeDescriptor>.Create(BoundAttributeDescriptorComparer.Default);

    private static readonly ObjectPool<HashSet<TagMatchingRuleDescriptor>> s_tagMatchingRuleSetPool
        = HashSetPool<TagMatchingRuleDescriptor>.Create(TagMatchingRuleDescriptorComparer.Default);

    private string? _kind;
    private string? _name;
    private string? _assemblyName;

    private DocumentationObject _documentationObject;

    private List<AllowedChildTagDescriptorBuilder>? _allowedChildTags;
    private List<BoundAttributeDescriptorBuilder>? _attributeBuilders;
    private List<TagMatchingRuleDescriptorBuilder>? _tagMatchingRuleBuilders;
    private ImmutableArray<RazorDiagnostic>.Builder? _diagnostics;
    private MetadataHolder _metadata;

    private TagHelperDescriptorBuilder()
    {
    }

    internal TagHelperDescriptorBuilder(string kind, string name, string assemblyName)
        : this()
    {
        _kind = kind ?? throw new ArgumentNullException(nameof(kind));
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _assemblyName = assemblyName ?? throw new ArgumentNullException(nameof(assemblyName));
    }

    public static TagHelperDescriptorBuilder Create(string name, string assemblyName)
        => new(TagHelperConventions.DefaultKind, name, assemblyName);

    public static TagHelperDescriptorBuilder Create(string kind, string name, string assemblyName)
        => new(kind, name, assemblyName);

    public string Kind => _kind.AssumeNotNull();
    public string Name => _name.AssumeNotNull();
    public string AssemblyName => _assemblyName.AssumeNotNull();
    public string? DisplayName { get; set; }
    public string? TagOutputHint { get; set; }
    public bool CaseSensitive { get; set; }

    public string? Documentation
    {
        get => _documentationObject.GetText();
        set => _documentationObject = new(value);
    }

    public IDictionary<string, string?> Metadata => _metadata.MetadataDictionary;

    public void SetMetadata(MetadataCollection metadata) => _metadata.SetMetadataCollection(metadata);

    public bool TryGetMetadataValue(string key, [NotNullWhen(true)] out string? value)
        => _metadata.TryGetMetadataValue(key, out value);

    public ImmutableArray<RazorDiagnostic>.Builder Diagnostics => _diagnostics ??= ImmutableArray.CreateBuilder<RazorDiagnostic>();

    public IReadOnlyList<AllowedChildTagDescriptorBuilder> AllowedChildTags
    {
        get
        {
            EnsureAllowedChildTags();

            return _allowedChildTags;
        }
    }

    public IReadOnlyList<BoundAttributeDescriptorBuilder> BoundAttributes
    {
        get
        {
            EnsureAttributeBuilders();

            return _attributeBuilders;
        }
    }

    public IReadOnlyList<TagMatchingRuleDescriptorBuilder> TagMatchingRules
    {
        get
        {
            EnsureTagMatchingRuleBuilders();

            return _tagMatchingRuleBuilders;
        }
    }

    public void AllowChildTag(Action<AllowedChildTagDescriptorBuilder> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        EnsureAllowedChildTags();

        var builder = AllowedChildTagDescriptorBuilder.GetInstance(this);
        configure(builder);
        _allowedChildTags.Add(builder);
    }

    public void BindAttribute(Action<BoundAttributeDescriptorBuilder> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        EnsureAttributeBuilders();

        var builder = BoundAttributeDescriptorBuilder.GetInstance(this, Kind);
        configure(builder);
        _attributeBuilders.Add(builder);
    }

    public void TagMatchingRule(Action<TagMatchingRuleDescriptorBuilder> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        EnsureTagMatchingRuleBuilders();

        var builder = TagMatchingRuleDescriptorBuilder.GetInstance(this);
        configure(builder);
        _tagMatchingRuleBuilders.Add(builder);
    }

    internal void SetDocumentation(string? text)
    {
        _documentationObject = new(text);
    }

    internal void SetDocumentation(DocumentationDescriptor? documentation)
    {
        _documentationObject = new(documentation);
    }

    public TagHelperDescriptor Build()
    {
        using var diagnostics = new PooledHashSet<RazorDiagnostic>();

        diagnostics.UnionWith(_diagnostics);

        var allowedChildTags = _allowedChildTags.BuildAllOrEmpty(s_allowedChildTagSetPool);
        var tagMatchingRules = _tagMatchingRuleBuilders.BuildAllOrEmpty(s_tagMatchingRuleSetPool);
        var attributes = _attributeBuilders.BuildAllOrEmpty(s_boundAttributeSetPool);

        _metadata.AddIfMissing(TagHelperMetadata.Runtime.Name, TagHelperConventions.DefaultKind);
        var metadata = _metadata.GetMetadataCollection();

        var descriptor = new TagHelperDescriptor(
            Kind,
            Name,
            AssemblyName,
            GetDisplayName(),
            _documentationObject,
            TagOutputHint,
            CaseSensitive,
            tagMatchingRules,
            attributes,
            allowedChildTags,
            metadata,
            diagnostics.ToImmutableArray());

        return descriptor;
    }

    internal string GetDisplayName()
    {
        return DisplayName ?? GetTypeName() ?? Name;

        string? GetTypeName()
        {
            return TryGetMetadataValue(TagHelperMetadata.Common.TypeName, out var value)
                ? value
                : null;
        }
    }

    [MemberNotNull(nameof(_allowedChildTags))]
    private void EnsureAllowedChildTags()
    {
        _allowedChildTags ??= new List<AllowedChildTagDescriptorBuilder>();
    }

    [MemberNotNull(nameof(_attributeBuilders))]
    private void EnsureAttributeBuilders()
    {
        _attributeBuilders ??= new List<BoundAttributeDescriptorBuilder>();
    }

    [MemberNotNull(nameof(_tagMatchingRuleBuilders))]
    private void EnsureTagMatchingRuleBuilders()
    {
        _tagMatchingRuleBuilders ??= new List<TagMatchingRuleDescriptorBuilder>();
    }

    internal MetadataBuilder GetMetadataBuilder(string? runtimeName = null)
    {
        var metadataBuilder = new MetadataBuilder();

        metadataBuilder.Add(TagHelperMetadata.Runtime.Name, runtimeName ?? TagHelperConventions.DefaultKind);

        return metadataBuilder;
    }
}
