// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

internal class DefaultRequiredAttributeDescriptorBuilder : RequiredAttributeDescriptorBuilder, IBuilder<RequiredAttributeDescriptor>
{
    private readonly DefaultTagMatchingRuleDescriptorBuilder _parent;
    private RazorDiagnosticCollection _diagnostics;
    private readonly Dictionary<string, string> _metadata = new Dictionary<string, string>();

    public DefaultRequiredAttributeDescriptorBuilder(DefaultTagMatchingRuleDescriptorBuilder parent)
    {
        _parent = parent;
    }

    public override string Name { get; set; }

    public override RequiredAttributeDescriptor.NameComparisonMode NameComparisonMode { get; set; }

    public override string Value { get; set; }

    public override RequiredAttributeDescriptor.ValueComparisonMode ValueComparisonMode { get; set; }

    public override RazorDiagnosticCollection Diagnostics => _diagnostics ??= new RazorDiagnosticCollection();

    public override IDictionary<string, string> Metadata => _metadata;

    internal bool CaseSensitive => _parent.CaseSensitive;

    public RequiredAttributeDescriptor Build()
    {
        using var _ = HashSetPool<RazorDiagnostic>.GetPooledObject(out var diagnostics);

        Validate(diagnostics);

        if (_diagnostics is { } existingDiagnostics)
        {
            diagnostics.UnionWith(existingDiagnostics);
        }

        var displayName = GetDisplayName();
        var rule = new DefaultRequiredAttributeDescriptor(
            Name,
            NameComparisonMode,
            CaseSensitive,
            Value,
            ValueComparisonMode,
            displayName,
            diagnostics.ToArray(),
            new Dictionary<string, string>(Metadata));

        return rule;
    }

    private string GetDisplayName()
    {
        return NameComparisonMode == RequiredAttributeDescriptor.NameComparisonMode.PrefixMatch ? string.Concat(Name, "...") : Name;
    }

    private void Validate(HashSet<RazorDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidTargetedAttributeNameNullOrWhitespace();

            diagnostics.Add(diagnostic);
        }
        else
        {
            var name = new StringSegment(Name);
            var isDirectiveAttribute = this.IsDirectiveAttribute();
            if (isDirectiveAttribute && name.StartsWith("@", StringComparison.Ordinal))
            {
                name = name.Subsegment(1);
            }
            else if (isDirectiveAttribute)
            {
                var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidRequiredDirectiveAttributeName(GetDisplayName(), Name);

                diagnostics.Add(diagnostic);
            }

            for (var i = 0; i < name.Length; i++)
            {
                var character = name[i];
                if (char.IsWhiteSpace(character) || HtmlConventions.IsInvalidNonWhitespaceHtmlCharacters(character))
                {
                    var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidTargetedAttributeName(Name, character);

                    diagnostics.Add(diagnostic);
                }
            }
        }
    }
}
