﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.CodeAnalysis.Razor;

internal class RefTagHelperDescriptorProvider : ITagHelperDescriptorProvider
{
    private static TagHelperDescriptor s_refTagHelper;

    // Run after the component tag helper provider, because later we may want component-type-specific variants of this
    public int Order { get; set; } = 1000;

    public RazorEngine Engine { get; set; }

    public void Execute(TagHelperDescriptorProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var compilation = context.GetCompilation();
        if (compilation == null)
        {
            return;
        }

        var elementReference = compilation.GetTypeByMetadataName(ComponentsApi.ElementReference.FullTypeName);
        if (elementReference == null)
        {
            // If we can't find ElementRef, then just bail. We won't be able to compile the
            // generated code anyway.
            return;
        }

        var targetSymbol = context.Items.GetTargetSymbol();
        if (targetSymbol is not null && !SymbolEqualityComparer.Default.Equals(targetSymbol, elementReference.ContainingAssembly))
        {
            return;
        }

        context.Results.Add(GetOrCreateRefTagHelper());
    }

    private static TagHelperDescriptor GetOrCreateRefTagHelper()
    {
        return s_refTagHelper ??= CreateRefTagHelper();

        static TagHelperDescriptor CreateRefTagHelper()
        {
            using var _ = TagHelperDescriptorBuilder.GetPooledInstance(
                ComponentMetadata.Ref.TagHelperKind, "Ref", ComponentsApi.AssemblyName,
                out var builder);

            builder.CaseSensitive = true;
            builder.SetDocumentation(DocumentationDescriptor.RefTagHelper);

            builder.SetMetadata(
                SpecialKind(ComponentMetadata.Ref.TagHelperKind),
                MakeTrue(TagHelperMetadata.Common.ClassifyAttributesOnly),
                RuntimeName(ComponentMetadata.Ref.RuntimeName),
                TypeName("Microsoft.AspNetCore.Components.Ref"));

            builder.TagMatchingRule(rule =>
            {
                rule.TagName = "*";
                rule.Attribute(attribute =>
                {
                    attribute.Name = "@ref";
                    attribute.SetMetadata(Attributes.IsDirectiveAttribute);
                });
            });

            builder.BindAttribute(attribute =>
            {
                attribute.SetDocumentation(DocumentationDescriptor.RefTagHelper);
                attribute.Name = "@ref";

                attribute.TypeName = typeof(object).FullName;
                attribute.SetMetadata(
                    PropertyName("Ref"),
                    IsDirectiveAttribute);
            });

            return builder.Build();
        }
    }
}
