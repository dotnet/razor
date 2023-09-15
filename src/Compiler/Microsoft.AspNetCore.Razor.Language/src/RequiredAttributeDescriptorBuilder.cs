// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;
using static Microsoft.AspNetCore.Razor.Language.RequiredAttributeDescriptor;

namespace Microsoft.AspNetCore.Razor.Language;

public partial class RequiredAttributeDescriptorBuilder : IBuilder<RequiredAttributeDescriptor>
{
    private static readonly ObjectPool<RequiredAttributeDescriptorBuilder> s_pool = DefaultPool.Create(Policy.Instance);

    internal static RequiredAttributeDescriptorBuilder GetInstance(DefaultTagMatchingRuleDescriptorBuilder parent)
    {
        var builder = s_pool.Get();

        builder._parent = parent;

        return builder;
    }

    internal static void ReturnInstance(RequiredAttributeDescriptorBuilder builder)
        => s_pool.Return(builder);

    [AllowNull]
    private DefaultTagMatchingRuleDescriptorBuilder _parent;
    private ImmutableArray<RazorDiagnostic>.Builder? _diagnostics;
    private MetadataHolder _metadata;

    private RequiredAttributeDescriptorBuilder()
    {
    }

    internal RequiredAttributeDescriptorBuilder(DefaultTagMatchingRuleDescriptorBuilder parent)
    {
        _parent = parent;
    }

    public string? Name { get; set; }
    public NameComparisonMode NameComparisonMode { get; set; }
    public string? Value { get; set; }
    public ValueComparisonMode ValueComparisonMode { get; set; }

    internal bool CaseSensitive => _parent.CaseSensitive;

    public ImmutableArray<RazorDiagnostic>.Builder Diagnostics => _diagnostics ??= ImmutableArray.CreateBuilder<RazorDiagnostic>();

    public IDictionary<string, string?> Metadata => _metadata.MetadataDictionary;

    public void SetMetadata(MetadataCollection metadata) => _metadata.SetMetadataCollection(metadata);

    public bool TryGetMetadataValue(string key, [NotNullWhen(true)] out string? value)
        => _metadata.TryGetMetadataValue(key, out value);

    public RequiredAttributeDescriptor Build()
    {
        var diagnostics = new PooledHashSet<RazorDiagnostic>();
        try
        {
            Validate(ref diagnostics);

            diagnostics.UnionWith(_diagnostics);

            var displayName = GetDisplayName();
            var metadata = _metadata.GetMetadataCollection();

            var descriptor = new RequiredAttributeDescriptor(
                Name!, // Name is not expected to be null. If it is, a diagnostic will be created for it.
                NameComparisonMode,
                CaseSensitive,
                Value,
                ValueComparisonMode,
                displayName,
                diagnostics.ToImmutableArray(),
                metadata);

            return descriptor;
        }
        finally
        {
            diagnostics.ClearAndFree();
        }
    }

    private string GetDisplayName()
    {
        return (NameComparisonMode == NameComparisonMode.PrefixMatch ? string.Concat(Name, "...") : Name) ?? string.Empty;
    }

    private bool IsDirectiveAttribute()
        => TryGetMetadataValue(ComponentMetadata.Common.DirectiveAttribute, out var value) &&
           value == bool.TrueString;

    private void Validate(ref PooledHashSet<RazorDiagnostic> diagnostics)
    {
        if (Name.IsNullOrWhiteSpace())
        {
            var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidTargetedAttributeNameNullOrWhitespace();

            diagnostics.Add(diagnostic);
        }
        else
        {
            var name = Name.AsSpan();
            var isDirectiveAttribute = IsDirectiveAttribute();
            if (isDirectiveAttribute && name[0] == '@')
            {
                name = name[1..];
            }
            else if (isDirectiveAttribute)
            {
                var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidRequiredDirectiveAttributeName(GetDisplayName(), Name);

                diagnostics.Add(diagnostic);
            }

            foreach (var ch in name)
            {
                if (char.IsWhiteSpace(ch) || HtmlConventions.IsInvalidNonWhitespaceHtmlCharacters(ch))
                {
                    var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidTargetedAttributeName(Name, ch);

                    diagnostics.Add(diagnostic);
                }
            }
        }
    }
}
