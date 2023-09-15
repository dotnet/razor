// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public sealed class TagMatchingRuleDescriptor : TagHelperObject, IEquatable<TagMatchingRuleDescriptor>
{
    private int? _hashCode;
    private ImmutableArray<RazorDiagnostic>? _allDiagnostics;

    public string TagName { get; }
    public string? ParentTag { get; }
    public TagStructure TagStructure { get; }
    public bool CaseSensitive => HasFlag(CaseSensitiveBit);
    public ImmutableArray<RequiredAttributeDescriptor> Attributes { get; }

    internal TagMatchingRuleDescriptor(
        string tagName,
        string? parentTag,
        TagStructure tagStructure,
        bool caseSensitive,
        ImmutableArray<RequiredAttributeDescriptor> attributes,
        ImmutableArray<RazorDiagnostic> diagnostics)
    {
        TagName = tagName;
        ParentTag = parentTag;
        TagStructure = tagStructure;
        SetOrClearFlag(CaseSensitiveBit, caseSensitive);
        Attributes = attributes.NullToEmpty();

        if (!diagnostics.IsDefaultOrEmpty)
        {
            SetFlag(ContainsDiagnosticsBit);
            TagHelperDiagnostics.AddDiagnostics(this, diagnostics);
        }
    }

    public ImmutableArray<RazorDiagnostic> Diagnostics
        => HasFlag(ContainsDiagnosticsBit)
            ? TagHelperDiagnostics.GetDiagnostics(this)
            : ImmutableArray<RazorDiagnostic>.Empty;

    public bool HasErrors
    {
        get
        {
            var allDiagnostics = GetAllDiagnostics();

            return allDiagnostics.Any(static d => d.Severity == RazorDiagnosticSeverity.Error);
        }
    }

    public ImmutableArray<RazorDiagnostic> GetAllDiagnostics()
    {
        return _allDiagnostics ??= GetAllDiagnosticsCore();

        ImmutableArray<RazorDiagnostic> GetAllDiagnosticsCore()
        { 
            using var diagnostics = new PooledArrayBuilder<RazorDiagnostic>();

            foreach (var attribute in Attributes)
            {
                diagnostics.AddRange(attribute.Diagnostics);
            }

            diagnostics.AddRange(Diagnostics);

            return diagnostics.ToImmutable();
        }
    }

    public bool Equals(TagMatchingRuleDescriptor other)
    {
        return TagMatchingRuleDescriptorComparer.Default.Equals(this, other);
    }

    public override bool Equals(object? obj)
        => obj is TagMatchingRuleDescriptor other &&
           Equals(other);

    public override int GetHashCode()
    {
        _hashCode ??= TagMatchingRuleDescriptorComparer.Default.GetHashCode(this);
        return _hashCode.GetValueOrDefault();
    }

    internal string GetDebuggerDisplay()
    {
        var tagName = TagName ?? "*";
        tagName += TagStructure == TagStructure.WithoutEndTag ? "/" : "";
        return $"{TagName ?? "*"}[{string.Join(", ", Attributes.Select(a => DescribeAttribute(a)))}]";
        static string DescribeAttribute(RequiredAttributeDescriptor attribute)
        {
            var name = attribute.Name switch
            {
                null => "*",
                var prefix when attribute.NameComparison == RequiredAttributeDescriptor.NameComparisonMode.PrefixMatch => $"^{prefix}",
                var full => full,
            };

            var value = attribute.Value switch
            {
                null => "",
                var prefix when attribute.ValueComparison == RequiredAttributeDescriptor.ValueComparisonMode.PrefixMatch => $"^={prefix}",
                var suffix when attribute.ValueComparison == RequiredAttributeDescriptor.ValueComparisonMode.SuffixMatch => $"$={suffix}",
                var full => $"={full}",
            };
            return name + value;
        }
    }
}
