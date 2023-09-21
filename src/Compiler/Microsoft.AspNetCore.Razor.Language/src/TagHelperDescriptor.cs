// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public sealed class TagHelperDescriptor : TagHelperObject, IEquatable<TagHelperDescriptor>
{
    private const int IsComponentFullyQualifiedNameMatchCacheSetBit = LastFlagBit << 1;
    private const int IsComponentFullyQualifiedNameMatchCacheBit = LastFlagBit << 2;
    private const int IsChildContentTagHelperCacheSetBit = LastFlagBit << 3;
    private const int IsChildContentTagHelperCacheBit = LastFlagBit << 4;

    private int? _hashCode;
    private readonly DocumentationObject _documentationObject;

    private ImmutableArray<BoundAttributeDescriptor>? _editorRequiredAttributes;

    public string Kind { get; }
    public string Name { get; }
    public string AssemblyName { get; }

    public string? Documentation => _documentationObject.GetText();
    internal DocumentationObject DocumentationObject => _documentationObject;

    public string DisplayName { get; }
    public string? TagOutputHint { get; }

    public bool CaseSensitive => HasFlag(CaseSensitiveBit);

    public ImmutableArray<AllowedChildTagDescriptor> AllowedChildTags { get; }
    public ImmutableArray<BoundAttributeDescriptor> BoundAttributes { get; }
    public ImmutableArray<TagMatchingRuleDescriptor> TagMatchingRules { get; }

    public MetadataCollection Metadata { get; }

    internal TagHelperDescriptor(
        string kind,
        string name,
        string assemblyName,
        string displayName,
        DocumentationObject documentationObject,
        string? tagOutputHint,
        bool caseSensitive,
        ImmutableArray<TagMatchingRuleDescriptor> tagMatchingRules,
        ImmutableArray<BoundAttributeDescriptor> attributeDescriptors,
        ImmutableArray<AllowedChildTagDescriptor> allowedChildTags,
        MetadataCollection metadata,
        ImmutableArray<RazorDiagnostic> diagnostics)
        : base(diagnostics)
    {
        Kind = kind;
        Name = name;
        AssemblyName = assemblyName;
        DisplayName = displayName;
        _documentationObject = documentationObject;
        TagOutputHint = tagOutputHint;
        SetOrClearFlag(CaseSensitiveBit, caseSensitive);
        TagMatchingRules = tagMatchingRules.NullToEmpty();
        BoundAttributes = attributeDescriptors.NullToEmpty();
        AllowedChildTags = allowedChildTags.NullToEmpty();
        Metadata = metadata;
    }

    internal bool? IsComponentFullyQualifiedNameMatchCache
    {
        get => GetTriStateFlags(isSetFlag: IsComponentFullyQualifiedNameMatchCacheSetBit, isOnFlag: IsComponentFullyQualifiedNameMatchCacheBit);
        set => UpdateTriStateFlags(value, isSetFlag: IsComponentFullyQualifiedNameMatchCacheSetBit, isOnFlag: IsComponentFullyQualifiedNameMatchCacheBit);
    }

    internal bool? IsChildContentTagHelperCache
    {
        get => GetTriStateFlags(isSetFlag: IsChildContentTagHelperCacheSetBit, isOnFlag: IsChildContentTagHelperCacheBit);
        set => UpdateTriStateFlags(value, isSetFlag: IsChildContentTagHelperCacheSetBit, isOnFlag: IsChildContentTagHelperCacheBit);
    }

    internal ImmutableArray<BoundAttributeDescriptor> EditorRequiredAttributes
    {
        get
        {
            return _editorRequiredAttributes ??= GetEditorRequiredAttributes(BoundAttributes);

            static ImmutableArray<BoundAttributeDescriptor> GetEditorRequiredAttributes(ImmutableArray<BoundAttributeDescriptor> attributes)
            {
                if (attributes.Length == 0)
                {
                    return ImmutableArray<BoundAttributeDescriptor>.Empty;
                }

                using var results = new PooledArrayBuilder<BoundAttributeDescriptor>(capacity: attributes.Length);

                foreach (var attribute in attributes)
                {
                    if (attribute is { IsEditorRequired: true } editorRequiredAttribute)
                    {
                        results.Add(editorRequiredAttribute);
                    }
                }

                return results.DrainToImmutable();
            }
        }
    }

    public IEnumerable<RazorDiagnostic> GetAllDiagnostics()
    {
        foreach (var allowedChildTag in AllowedChildTags)
        {
            foreach (var diagnostic in allowedChildTag.Diagnostics)
            {
                yield return diagnostic;
            }
        }

        foreach (var boundAttribute in BoundAttributes)
        {
            foreach (var diagnostic in boundAttribute.Diagnostics)
            {
                yield return diagnostic;
            }
        }

        foreach (var tagMatchingRule in TagMatchingRules)
        {
            foreach (var diagnostic in tagMatchingRule.Diagnostics)
            {
                yield return diagnostic;
            }
        }

        foreach (var diagnostic in Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public override string ToString()
    {
        return DisplayName ?? base.ToString();
    }

    public bool Equals(TagHelperDescriptor other)
    {
        return TagHelperDescriptorComparer.Default.Equals(this, other);
    }

    public override bool Equals(object? obj)
    {
        return obj is TagHelperDescriptor other &&
               Equals(other);
    }

    public override int GetHashCode()
    {
        // TagHelperDescriptors are immutable instances and it should be safe to cache it's hashes once.
        return _hashCode ??= TagHelperDescriptorComparer.Default.GetHashCode(this);
    }

    private string GetDebuggerDisplay()
    {
        return $"{DisplayName} - {string.Join(" | ", TagMatchingRules.Select(r => r.GetDebuggerDisplay()))}";
    }

    internal TagHelperDescriptor WithName(string name)
    {
        return new(
            Kind, name, AssemblyName, DisplayName,
            DocumentationObject, TagOutputHint, CaseSensitive,
            TagMatchingRules, BoundAttributes, AllowedChildTags,
            Metadata, Diagnostics);
    }
}
