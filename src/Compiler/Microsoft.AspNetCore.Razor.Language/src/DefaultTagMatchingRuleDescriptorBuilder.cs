// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

internal class DefaultTagMatchingRuleDescriptorBuilder : TagMatchingRuleDescriptorBuilder, IBuilder<TagMatchingRuleDescriptor>
{
    private static readonly ObjectPool<HashSet<RequiredAttributeDescriptor>> s_requiredAttributeSetPool
        = HashSetPool<RequiredAttributeDescriptor>.Create(RequiredAttributeDescriptorComparer.Default);

    private readonly DefaultTagHelperDescriptorBuilder _parent;
    private List<DefaultRequiredAttributeDescriptorBuilder>? _requiredAttributeBuilders;
    private RazorDiagnosticCollection? _diagnostics;

    internal DefaultTagMatchingRuleDescriptorBuilder(DefaultTagHelperDescriptorBuilder parent)
    {
        _parent = parent;
    }

    public override string? TagName { get; set; }

    public override string? ParentTag { get; set; }

    public override TagStructure TagStructure { get; set; }

    internal bool CaseSensitive => _parent.CaseSensitive;

    public override RazorDiagnosticCollection Diagnostics => _diagnostics ??= new RazorDiagnosticCollection();

    public override IReadOnlyList<RequiredAttributeDescriptorBuilder> Attributes
    {
        get
        {
            EnsureRequiredAttributeBuilders();

            return _requiredAttributeBuilders;
        }
    }

    public override void Attribute(Action<RequiredAttributeDescriptorBuilder> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        EnsureRequiredAttributeBuilders();

        var builder = new DefaultRequiredAttributeDescriptorBuilder(this);
        configure(builder);
        _requiredAttributeBuilders.Add(builder);
    }

    public TagMatchingRuleDescriptor Build()
    {
        using var _ = HashSetPool<RazorDiagnostic>.GetPooledObject(out var diagnostics);

        Validate(diagnostics);

        if (_diagnostics is { } existingDiagnostics)
        {
            diagnostics.UnionWith(existingDiagnostics);
        }

        var requiredAttributes = _requiredAttributeBuilders is { } requiredAttributeBuilders
            ? requiredAttributeBuilders.BuildAll(s_requiredAttributeSetPool)
            : Array.Empty<RequiredAttributeDescriptor>();

        var rule = new DefaultTagMatchingRuleDescriptor(
            TagName,
            ParentTag,
            TagStructure,
            CaseSensitive,
            requiredAttributes,
            diagnostics.ToArray());

        return rule;
    }

    private void Validate(HashSet<RazorDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(TagName))
        {
            var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidTargetedTagNameNullOrWhitespace();

            diagnostics.Add(diagnostic);
        }
        else if (TagName != TagHelperMatchingConventions.ElementCatchAllName)
        {
            foreach (var character in TagName!)
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
            if (string.IsNullOrWhiteSpace(ParentTag))
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
        _requiredAttributeBuilders ??= new List<DefaultRequiredAttributeDescriptorBuilder>();
    }
}
