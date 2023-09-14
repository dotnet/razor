// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

public partial class AllowedChildTagDescriptorBuilder : IBuilder<AllowedChildTagDescriptor>
{
    private static readonly ObjectPool<AllowedChildTagDescriptorBuilder> s_pool = DefaultPool.Create(Policy.Instance);

    internal static AllowedChildTagDescriptorBuilder GetInstance(DefaultTagHelperDescriptorBuilder parent)
    {
        var builder = s_pool.Get();

        builder._parent = parent;

        return builder;
    }

    internal static void ReturnInstance(AllowedChildTagDescriptorBuilder builder)
        => s_pool.Return(builder);

    [AllowNull]
    private DefaultTagHelperDescriptorBuilder _parent;
    private ImmutableArray<RazorDiagnostic>.Builder? _diagnostics;

    private AllowedChildTagDescriptorBuilder()
    {
    }

    internal AllowedChildTagDescriptorBuilder(DefaultTagHelperDescriptorBuilder parent)
    {
        _parent = parent;
    }

    public string? Name { get; set; }
    public string? DisplayName { get; set; }

    public ImmutableArray<RazorDiagnostic>.Builder Diagnostics
        => _diagnostics ??= ImmutableArray.CreateBuilder<RazorDiagnostic>();

    public AllowedChildTagDescriptor Build()
    {
        var diagnostics = new PooledHashSet<RazorDiagnostic>();
        try
        {
            Validate(ref diagnostics);

            diagnostics.UnionWith(_diagnostics);

            var displayName = DisplayName ?? Name ?? string.Empty;

            var descriptor = new AllowedChildTagDescriptor(
                Name!, // Name is not expected to be null. If it is, a diagnostic will be created for it.
                displayName,
                diagnostics.ToImmutableArray());

            return descriptor;
        }
        finally
        {
            diagnostics.ClearAndFree();
        }
    }

    private void Validate(ref PooledHashSet<RazorDiagnostic> diagnostics)
    {
        if (Name.IsNullOrWhiteSpace())
        {
            var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidRestrictedChildNullOrWhitespace(_parent.GetDisplayName());

            diagnostics.Add(diagnostic);
        }
        else if (Name != TagHelperMatchingConventions.ElementCatchAllName)
        {
            foreach (var character in Name)
            {
                if (char.IsWhiteSpace(character) || HtmlConventions.IsInvalidNonWhitespaceHtmlCharacters(character))
                {
                    var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidRestrictedChild(_parent.GetDisplayName(), Name, character);

                    diagnostics.Add(diagnostic);
                }
            }
        }
    }
}
