// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.TagHelpers.Producers;

namespace Microsoft.CodeAnalysis.Razor;

internal sealed class BindTagHelperDescriptorProvider() : TagHelperDescriptorProviderBase
{
    private static readonly Lazy<TagHelperDescriptor> s_fallbackBindTagHelper = new(CreateFallbackBindTagHelper);

    public override void Execute(TagHelperDescriptorProviderContext context, CancellationToken cancellationToken = default)
    {
        ArgHelper.ThrowIfNull(context);

        // This provider returns tag helper information for 'bind' which doesn't necessarily
        // map to any real component. Bind behaviors more like a macro, which can map a single LValue to
        // both a 'value' attribute and a 'value changed' attribute.
        //
        // User types:
        //      <input type="text" @bind="@FirstName"/>
        //
        // We generate:
        //      <input type="text"
        //          value="@BindMethods.GetValue(FirstName)"
        //          onchange="@EventCallbackFactory.CreateBinder(this, __value => FirstName = __value, FirstName)"/>
        //
        // This isn't very different from code the user could write themselves - thus the pronouncement
        // that @bind is very much like a macro.
        //
        // A lot of the value that provide in this case is that the associations between the
        // elements, and the attributes aren't straightforward.
        //
        // For instance on <input type="text" /> we need to listen to 'value' and 'onchange',
        // but on <input type="checked" we need to listen to 'checked' and 'onchange'.
        //
        // We handle a few different cases here:
        //
        //  1.  When given an attribute like **anywhere**'@bind-value="@FirstName"' and '@bind-value:event="onchange"' we will
        //      generate the 'value' attribute and 'onchange' attribute.
        //
        //      We don't do any transformation or inference for this case, because the developer has
        //      told us exactly what to do. This is the *full* form of @bind, and should support any
        //      combination of element, component, and attributes.
        //
        //      This is the most general case, and is implemented with a built-in tag helper that applies
        //      to everything, and binds to a dictionary of attributes that start with @bind-.
        //
        //  2.  We also support cases like '@bind-value="@FirstName"' where we will generate the 'value'
        //      attribute and another attribute based for a changed handler based on the metadata.
        //
        //      These mappings are provided by attributes that tell us what attributes, suffixes, and
        //      elements to map.
        //
        //  3.  When given an attribute like '@bind="@FirstName"' we will generate a value and change
        //      attribute solely based on the context. We need the context of an HTML tag to know
        //      what attributes to generate.
        //
        //      Similar to case #2, this should 'just work' from the users point of view. We expect
        //      using this syntax most frequently with input elements.
        //
        //      These mappings are also provided by attributes. Primarily these are used by <input />
        //      and so we have a special case for input elements and their type attributes.
        //
        //      Additionally, our mappings tell us about cases like <input type="number" ... /> where
        //      we need to treat the value as an invariant culture value. In general the HTML5 field
        //      types use invariant culture values when interacting with the DOM, in contrast to
        //      <input type="text" ... /> which is free-form text and is most likely to be
        //      culture-sensitive.
        //
        //  4.  For components, we have a bit of a special case. We can infer a syntax that matches
        //      case #2 based on property names. So if a component provides both 'Value' and 'ValueChanged'
        //      we will turn that into an instance of bind.
        //
        // So case #1 here is the most general case. Case #2 and #3 are data-driven based on attribute data
        // we have. Case #4 is data-driven based on component definitions.
        //
        // We provide a good set of attributes that map to the HTML dom. This set is user extensible.
        var compilation = context.Compilation;
        var targetAssembly = context.TargetAssembly;

        // If we can't produce bind tag helpers, there's no need in carrying on.
        if (!BindTagHelperProducer.TryCreate(compilation, out var producer))
        {
            return;
        }

        if (targetAssembly is not null &&
            !SymbolEqualityComparer.Default.Equals(targetAssembly, producer.BindConverterType.ContainingAssembly))
        {
            return;
        }

        // Tag Helper definition for case #1. This is the most general case.
        context.Results.Add(s_fallbackBindTagHelper.Value);

        if (!producer.CanProduceTagHelpers)
        {
            return;
        }

        // We want to walk the compilation and its references, not the target symbol.
        var collector = new Collector(compilation, producer);
        collector.Collect(context, cancellationToken);
    }

    private class Collector(Compilation compilation, BindTagHelperProducer producer)
        : TagHelperCollector<Collector>(compilation, targetAssembly: null)
    {
        protected override bool IsCandidateType(INamedTypeSymbol type)
            => producer.IsCandidateType(type);

        protected override void Collect(
            INamedTypeSymbol type,
            ICollection<TagHelperDescriptor> results,
            CancellationToken cancellationToken)
            => producer.ProduceTagHelpers(type, results, cancellationToken);
    }

    private static TagHelperDescriptor CreateFallbackBindTagHelper()
    {
        using var _ = TagHelperDescriptorBuilder.GetPooledInstance(
            TagHelperKind.Bind, "Bind", ComponentsApi.AssemblyName,
            out var builder);

        builder.SetTypeName(
            fullName: "Microsoft.AspNetCore.Components.Bind",
            typeNamespace: "Microsoft.AspNetCore.Components",
            typeNameIdentifier: "Bind");

        builder.CaseSensitive = true;
        builder.ClassifyAttributesOnly = true;
        builder.SetDocumentation(DocumentationDescriptor.BindTagHelper_Fallback);

        builder.SetMetadata(new BindMetadata() { IsFallback = true });

        builder.TagMatchingRule(rule =>
        {
            rule.TagName = "*";
            rule.Attribute(attribute =>
            {
                attribute.Name = "@bind-";
                attribute.NameComparison = RequiredAttributeNameComparison.PrefixMatch;
                attribute.IsDirectiveAttribute = true;
            });
        });

        builder.BindAttribute(attribute =>
        {
            attribute.SetDocumentation(DocumentationDescriptor.BindTagHelper_Fallback);

            var attributeName = "@bind-...";
            attribute.Name = attributeName;
            attribute.AsDictionary("@bind-", typeof(object).FullName);
            attribute.IsDirectiveAttribute = true;

            attribute.PropertyName = "Bind";

            attribute.TypeName = "System.Collections.Generic.Dictionary<string, object>";

            attribute.BindAttributeParameter(parameter =>
            {
                parameter.Name = "format";
                parameter.PropertyName = "Format";
                parameter.TypeName = typeof(string).FullName;
                parameter.SetDocumentation(DocumentationDescriptor.BindTagHelper_Fallback_Format);
            });

            attribute.BindAttributeParameter(parameter =>
            {
                parameter.Name = "event";
                parameter.PropertyName = "Event";
                parameter.TypeName = typeof(string).FullName;
                parameter.SetDocumentation(
                    DocumentationDescriptor.From(
                        DocumentationId.BindTagHelper_Fallback_Event, attributeName));
            });

            attribute.BindAttributeParameter(parameter =>
            {
                parameter.Name = "culture";
                parameter.PropertyName = "Culture";
                parameter.TypeName = typeof(CultureInfo).FullName;
                parameter.SetDocumentation(DocumentationDescriptor.BindTagHelper_Element_Culture);
            });

            attribute.BindAttributeParameter(parameter =>
            {
                parameter.Name = "get";
                parameter.PropertyName = "Get";
                parameter.TypeName = typeof(object).FullName;
                parameter.SetDocumentation(DocumentationDescriptor.BindTagHelper_Element_Get);
                parameter.BindAttributeGetSet = true;
            });

            attribute.BindAttributeParameter(parameter =>
            {
                parameter.Name = "set";
                parameter.PropertyName = "Set";
                parameter.TypeName = typeof(Delegate).FullName;
                parameter.SetDocumentation(DocumentationDescriptor.BindTagHelper_Element_Set);
            });

            attribute.BindAttributeParameter(parameter =>
            {
                parameter.Name = "after";
                parameter.PropertyName = "After";
                parameter.TypeName = typeof(Delegate).FullName;
                parameter.SetDocumentation(DocumentationDescriptor.BindTagHelper_Element_After);
            });
        });

        return builder.Build();
    }
}
