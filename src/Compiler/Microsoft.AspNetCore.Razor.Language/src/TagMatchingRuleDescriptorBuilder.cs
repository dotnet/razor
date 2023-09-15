// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

public partial class TagMatchingRuleDescriptorBuilder : IBuilder<TagMatchingRuleDescriptor>
{
    private static readonly ObjectPool<TagMatchingRuleDescriptorBuilder> s_pool = DefaultPool.Create(Policy.Instance);

    internal static TagMatchingRuleDescriptorBuilder GetInstance(DefaultTagHelperDescriptorBuilder parent)
    {
        var builder = s_pool.Get();

        builder._parent = parent;

        return builder;
    }

    internal static void ReturnInstance(TagMatchingRuleDescriptorBuilder builder)
        => s_pool.Return(builder);

    private static readonly ObjectPool<HashSet<RequiredAttributeDescriptor>> s_requiredAttributeSetPool
        = HashSetPool<RequiredAttributeDescriptor>.Create(RequiredAttributeDescriptorComparer.Default);

    [AllowNull]
    private DefaultTagHelperDescriptorBuilder _parent;
    private List<RequiredAttributeDescriptorBuilder>? _requiredAttributeBuilders;
    private ImmutableArray<RazorDiagnostic>.Builder? _diagnostics;

    private TagMatchingRuleDescriptorBuilder()
    {
    }

    internal TagMatchingRuleDescriptorBuilder(DefaultTagHelperDescriptorBuilder parent)
    {
        _parent = parent;
    }

    public string? TagName { get; set; }
    public string? ParentTag { get; set; }
    public TagStructure TagStructure { get; set; }

    internal bool CaseSensitive => _parent.CaseSensitive;

    public ImmutableArray<RazorDiagnostic>.Builder Diagnostics => _diagnostics ??= ImmutableArray.CreateBuilder<RazorDiagnostic>();

    public void Attribute(Action<RequiredAttributeDescriptorBuilder> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        EnsureRequiredAttributeBuilders();

        var builder = RequiredAttributeDescriptorBuilder.GetInstance(this);
        configure(builder);
        _requiredAttributeBuilders.Add(builder);
    }

    public TagMatchingRuleDescriptor Build()
    {
        var diagnostics = new PooledHashSet<RazorDiagnostic>();
        try
        {
            Validate(ref diagnostics);

            diagnostics.UnionWith(_diagnostics);

            var requiredAttributes = _requiredAttributeBuilders.BuildAllOrEmpty(s_requiredAttributeSetPool);

            var rule = new TagMatchingRuleDescriptor(
                TagName!, // TagName is not expected to be null. If it is, a diagnostic will be created for it.
                ParentTag,
                TagStructure,
                CaseSensitive,
                requiredAttributes,
                diagnostics.ToImmutableArray());

            return rule;
        }
        finally
        {
            diagnostics.ClearAndFree();
        }
    }

    private void Validate(ref PooledHashSet<RazorDiagnostic> diagnostics)
    {
        if (TagName.IsNullOrWhiteSpace())
        {
            var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidTargetedTagNameNullOrWhitespace();

            diagnostics.Add(diagnostic);
        }
        else if (TagName != TagHelperMatchingConventions.ElementCatchAllName)
        {
            foreach (var character in TagName)
            {
                if (char.IsWhiteSpace(character) || HtmlConventions.IsInvalidNonWhitespaceHtmlCharacters(character))
                {
                    var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidTargetedTagName(TagName, character);

                    diagnostics.Add(diagnostic);
                }
            }
        }

        if (ParentTag != null)
        {
            if (ParentTag.IsNullOrWhiteSpace())
            {
                var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidTargetedParentTagNameNullOrWhitespace();

                diagnostics.Add(diagnostic);
            }
            else
            {
                foreach (var character in ParentTag)
                {
                    if (char.IsWhiteSpace(character) || HtmlConventions.IsInvalidNonWhitespaceHtmlCharacters(character))
                    {
                        var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidTargetedParentTagName(ParentTag, character);

                        diagnostics.Add(diagnostic);
                    }
                }
            }
        }
    }

    [MemberNotNull(nameof(_requiredAttributeBuilders))]
    private void EnsureRequiredAttributeBuilders()
    {
        _requiredAttributeBuilders ??= new List<RequiredAttributeDescriptorBuilder>();
    }
}
