// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.CodeAnalysis.Razor;

internal class RenderModeTagHelperDescriptorProvider : ITagHelperDescriptorProvider
{
    private static TagHelperDescriptor? s_refTagHelper;

    // Run after the component tag helper provider
    public int Order { get; set; } = 1000;

    public RazorEngine? Engine { get; set; }

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

        var iComponentRenderMode = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.IComponentRenderMode"); // PROTOTYPE: Use (ComponentsApi.IComponentRenderMode.FullTypeName);
        if (iComponentRenderMode == null)
        {
            // If we can't find IComponentRenderMode, then just bail. We won't be able to compile the
            // generated code anyway.
            return;
        }

        var targetSymbol = context.Items.GetTargetSymbol();
        if (targetSymbol is not null && !SymbolEqualityComparer.Default.Equals(targetSymbol, iComponentRenderMode.ContainingAssembly))
        {
            return;
        }

        context.Results.Add(GetOrCreateRenderModeTagHelper());
    }

    private static TagHelperDescriptor GetOrCreateRenderModeTagHelper()
    {
        return s_refTagHelper ??= CreateRenderModeTagHelper();

        static TagHelperDescriptor CreateRenderModeTagHelper()
        {
            using var _ = TagHelperDescriptorBuilder.GetPooledInstance(
                ComponentMetadata.RenderMode.TagHelperKind, "RenderMode", ComponentsApi.AssemblyName,
                out var builder);

            builder.CaseSensitive = true;

            // PROTOYPE: docs
            //builder.SetDocumentation(DocumentationDescriptor.RenderModeTagHelper);

            builder.SetMetadata(
                SpecialKind(ComponentMetadata.RenderMode.TagHelperKind),
                MakeTrue(TagHelperMetadata.Common.ClassifyAttributesOnly),
                RuntimeName(ComponentMetadata.RenderMode.RuntimeName),
                TypeName("Microsoft.AspNetCore.Components.RenderMode"));

            // PROTOTYPE: so we need both, right? TagMatchingRule allows us to say 'this tag helper matches any element with a @rendermode attribute.
            //            BindAttribute causes the actual 'model binding' to happen that allows us to access it as a boundAttribute

            builder.TagMatchingRule(rule =>
            {
                rule.TagName = "*";
                rule.Attribute(attribute =>
                {
                    attribute.Name = "@rendermode";
                    attribute.SetMetadata(Attributes.IsDirectiveAttribute);
                });
            });

            builder.BindAttribute(attribute =>
            {
                // PROTOTYPE: docs
                //attribute.SetDocumentation(DocumentationDescriptor.RefTagHelper);
                attribute.Name = "@rendermode";

                attribute.TypeName = "Microsoft.AspNetCore.Components.IComponentRenderMode"; // PROTOTYPE: Extract out consts
                attribute.SetMetadata(
                    PropertyName("RenderMode"), // PROTOTYPE: are we using this metadata anywhere?
                    IsDirectiveAttribute); 
            });

            return builder.Build();
        }
    }
}
