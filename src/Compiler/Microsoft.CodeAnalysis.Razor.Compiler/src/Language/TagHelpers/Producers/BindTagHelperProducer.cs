// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.Language.TagHelpers.Producers;

internal sealed class BindTagHelperProducer
{
    public INamedTypeSymbol BindConverterType { get; }

    private readonly INamedTypeSymbol? _bindElementAttributeType;
    private readonly INamedTypeSymbol? _bindInputElementAttributeType;

    public bool CanProduceTagHelpers
        => _bindElementAttributeType is not null && _bindInputElementAttributeType is not null;

    private BindTagHelperProducer(
        INamedTypeSymbol bindConverterType,
        INamedTypeSymbol? bindElementAttributeType,
        INamedTypeSymbol? bindInputElementAttributeType)
    {
        BindConverterType = bindConverterType;
        _bindElementAttributeType = bindElementAttributeType;
        _bindInputElementAttributeType = bindInputElementAttributeType;
    }

    public static bool TryCreate(Compilation compilation, [NotNullWhen(true)] out BindTagHelperProducer? result)
    {
        if (!compilation.TryGetTypeByMetadataName(ComponentsApi.BindConverter.FullTypeName, out var bindConverterType))
        {
            result = null;
            return false;
        }

        var bindElementAttributeType = compilation.GetTypeByMetadataName(ComponentsApi.BindElementAttribute.FullTypeName);
        var bindInputElementAttributeType = compilation.GetTypeByMetadataName(ComponentsApi.BindInputElementAttribute.FullTypeName);

        result = new(bindConverterType, bindElementAttributeType, bindInputElementAttributeType);
        return true;
    }

    public bool IsCandidateType(INamedTypeSymbol type)
        => type.DeclaredAccessibility == Accessibility.Public &&
            type.Name == "BindAttributes";

    public void ProduceTagHelpers(INamedTypeSymbol type, ICollection<TagHelperDescriptor> results, CancellationToken cancellationToken)
    {
        if (!IsCandidateType(type))
        {
            return;
        }

        // Not handling duplicates here for now since we're the primary ones extending this.
        // If we see users adding to the set of 'bind' constructs we will want to add deduplication
        // and potentially diagnostics.
        foreach (var attribute in type.GetAttributes())
        {
            var constructorArguments = attribute.ConstructorArguments;

            TagHelperDescriptor? tagHelper = null;

            // For case #2 & #3 we have a whole bunch of attribute entries on BindMethods that we can use
            // to data-drive the definitions of these tag helpers.

            // We need to check the constructor argument length here, because this can show up as 0
            // if the language service fails to initialize. This is an invalid case, so skip it.
            if (constructorArguments.Length == 4 && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _bindElementAttributeType))
            {
                tagHelper = CreateElementBindTagHelper(
                    typeName: type.GetDefaultDisplayString(),
                    typeNamespace: type.ContainingNamespace.GetFullName(),
                    typeNameIdentifier: type.Name,
                    element: (string?)constructorArguments[0].Value,
                    typeAttribute: null,
                    suffix: (string?)constructorArguments[1].Value,
                    valueAttribute: (string?)constructorArguments[2].Value,
                    changeAttribute: (string?)constructorArguments[3].Value);
            }
            else if (constructorArguments.Length == 4 && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _bindInputElementAttributeType))
            {
                tagHelper = CreateElementBindTagHelper(
                    typeName: type.GetDefaultDisplayString(),
                    typeNamespace: type.ContainingNamespace.GetFullName(),
                    typeNameIdentifier: type.Name,
                    element: "input",
                    typeAttribute: (string?)constructorArguments[0].Value,
                    suffix: (string?)constructorArguments[1].Value,
                    valueAttribute: (string?)constructorArguments[2].Value,
                    changeAttribute: (string?)constructorArguments[3].Value);
            }
            else if (constructorArguments.Length == 6 && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _bindInputElementAttributeType))
            {
                tagHelper = CreateElementBindTagHelper(
                    typeName: type.GetDefaultDisplayString(),
                    typeNamespace: type.ContainingNamespace.GetFullName(),
                    typeNameIdentifier: type.Name,
                    element: "input",
                    typeAttribute: (string?)constructorArguments[0].Value,
                    suffix: (string?)constructorArguments[1].Value,
                    valueAttribute: (string?)constructorArguments[2].Value,
                    changeAttribute: (string?)constructorArguments[3].Value,
                    isInvariantCulture: (bool?)constructorArguments[4].Value ?? false,
                    format: (string?)constructorArguments[5].Value);
            }

            if (tagHelper is not null)
            {
                results.Add(tagHelper);
            }
        }
    }

    private static TagHelperDescriptor CreateElementBindTagHelper(
        string typeName,
        string typeNamespace,
        string typeNameIdentifier,
        string? element,
        string? typeAttribute,
        string? suffix,
        string? valueAttribute,
        string? changeAttribute,
        bool isInvariantCulture = false,
        string? format = null)
    {
        string name, attributeName, formatName, formatAttributeName, eventName;

        if (suffix is { } s)
        {
            name = "Bind_" + s;
            attributeName = "@bind-" + s;
            formatName = "Format_" + s;
            formatAttributeName = "format-" + s;
            eventName = "Event_" + s;
        }
        else
        {
            name = "Bind";
            attributeName = "@bind";

            suffix = valueAttribute;
            formatName = "Format_" + suffix;
            formatAttributeName = "format-" + suffix;
            eventName = "Event_" + suffix;
        }

        using var _ = TagHelperDescriptorBuilder.GetPooledInstance(
            TagHelperKind.Bind, name, ComponentsApi.AssemblyName,
            out var builder);

        builder.SetTypeName(typeName, typeNamespace, typeNameIdentifier);

        builder.CaseSensitive = true;
        builder.ClassifyAttributesOnly = true;
        builder.SetDocumentation(
            DocumentationDescriptor.From(
                DocumentationId.BindTagHelper_Element,
                valueAttribute,
                changeAttribute));

        var metadata = new BindMetadata.Builder
        {
            ValueAttribute = valueAttribute,
            ChangeAttribute = changeAttribute,
            IsInvariantCulture = isInvariantCulture,
            Format = format
        };

        if (typeAttribute != null)
        {
            // For entries that map to the <input /> element, we need to be able to know
            // the difference between <input /> and <input type="text" .../> for which we
            // want to use the same attributes.
            //
            // We provide a tag helper for <input /> that should match all input elements,
            // but we only want it to be used when a more specific one is used.
            //
            // Therefore we use this metadata to know which one is more specific when two
            // tag helpers match.
            metadata.TypeAttribute = typeAttribute;
        }

        builder.SetMetadata(metadata.Build());

        builder.TagMatchingRule(rule =>
        {
            rule.TagName = element;
            if (typeAttribute != null)
            {
                rule.Attribute(a =>
                {
                    a.Name = "type";
                    a.NameComparison = RequiredAttributeNameComparison.FullMatch;
                    a.Value = typeAttribute;
                    a.ValueComparison = RequiredAttributeValueComparison.FullMatch;
                });
            }

            rule.Attribute(a =>
            {
                a.Name = attributeName;
                a.NameComparison = RequiredAttributeNameComparison.FullMatch;
                a.IsDirectiveAttribute = true;
            });
        });

        builder.TagMatchingRule(rule =>
        {
            rule.TagName = element;
            if (typeAttribute != null)
            {
                rule.Attribute(a =>
                {
                    a.Name = "type";
                    a.NameComparison = RequiredAttributeNameComparison.FullMatch;
                    a.Value = typeAttribute;
                    a.ValueComparison = RequiredAttributeValueComparison.FullMatch;
                });
            }

            rule.Attribute(a =>
            {
                a.Name = $"{attributeName}:get";
                a.NameComparison = RequiredAttributeNameComparison.FullMatch;
                a.IsDirectiveAttribute = true;
            });

            rule.Attribute(a =>
            {
                a.Name = $"{attributeName}:set";
                a.NameComparison = RequiredAttributeNameComparison.FullMatch;
                a.IsDirectiveAttribute = true;
            });
        });

        builder.BindAttribute(a =>
        {
            a.SetDocumentation(
                DocumentationDescriptor.From(
                    DocumentationId.BindTagHelper_Element,
                    valueAttribute,
                    changeAttribute));

            a.Name = attributeName;
            a.TypeName = typeof(object).FullName;
            a.IsDirectiveAttribute = true;
            a.PropertyName = name;

            a.BindAttributeParameter(parameter =>
            {
                parameter.Name = "format";
                parameter.PropertyName = formatName;
                parameter.TypeName = typeof(string).FullName;
                parameter.SetDocumentation(
                    DocumentationDescriptor.From(
                        DocumentationId.BindTagHelper_Element_Format,
                        attributeName));
            });

            a.BindAttributeParameter(parameter =>
            {
                parameter.Name = "event";
                parameter.PropertyName = eventName;
                parameter.TypeName = typeof(string).FullName;
                parameter.SetDocumentation(
                    DocumentationDescriptor.From(
                        DocumentationId.BindTagHelper_Element_Event,
                        attributeName));
            });

            a.BindAttributeParameter(parameter =>
            {
                parameter.Name = "culture";
                parameter.PropertyName = "Culture";
                parameter.TypeName = typeof(CultureInfo).FullName;
                parameter.SetDocumentation(DocumentationDescriptor.BindTagHelper_Element_Culture);
            });

            a.BindAttributeParameter(parameter =>
            {
                parameter.Name = "get";
                parameter.PropertyName = "Get";
                parameter.TypeName = typeof(object).FullName;
                parameter.SetDocumentation(DocumentationDescriptor.BindTagHelper_Element_Get);
                parameter.BindAttributeGetSet = true;
            });

            a.BindAttributeParameter(parameter =>
            {
                parameter.Name = "set";
                parameter.PropertyName = "Set";
                parameter.TypeName = typeof(Delegate).FullName;
                parameter.SetDocumentation(DocumentationDescriptor.BindTagHelper_Element_Set);
            });

            a.BindAttributeParameter(parameter =>
            {
                parameter.Name = "after";
                parameter.PropertyName = "After";
                parameter.TypeName = typeof(Delegate).FullName;
                parameter.SetDocumentation(DocumentationDescriptor.BindTagHelper_Element_After);
            });
        });

        // This is no longer supported. This is just here so we can add a diagnostic later on when this matches.
        builder.BindAttribute(attribute =>
        {
            attribute.Name = formatAttributeName;
            attribute.TypeName = "System.String";
            attribute.SetDocumentation(
                DocumentationDescriptor.From(
                    DocumentationId.BindTagHelper_Element_Format,
                    attributeName));

            attribute.PropertyName = formatName;
        });

        return builder.Build();
    }

    public void ProduceTagHelpersForComponent(TagHelperDescriptor tagHelper, ICollection<TagHelperDescriptor> results)
    {
        if (tagHelper.Kind != TagHelperKind.Component || !CanProduceTagHelpers)
        {
            return;
        }

        // We want to create a 'bind' tag helper everywhere we see a pair of properties like `Foo`, `FooChanged`
        // where `FooChanged` is a delegate and `Foo` is not.
        //
        // The easiest way to figure this out without a lot of backtracking is to look for `FooChanged` and then
        // try to find a matching "Foo".
        //
        // We also look for a corresponding FooExpression attribute, though its presence is optional.
        foreach (var changeAttribute in tagHelper.BoundAttributes)
        {
            if (!changeAttribute.Name.EndsWith("Changed", StringComparison.Ordinal) ||

                // Allow the ValueChanged attribute to be a delegate or EventCallback<>.
                //
                // We assume that the Delegate or EventCallback<> has a matching type, and the C# compiler will help
                // you figure figure it out if you did it wrongly.
                (!changeAttribute.IsDelegateProperty() && !changeAttribute.IsEventCallbackProperty()))
            {
                continue;
            }

            BoundAttributeDescriptor? valueAttribute = null;
            BoundAttributeDescriptor? expressionAttribute = null;

            var valueAttributeName = changeAttribute.Name[..^"Changed".Length];
            var expressionAttributeName = valueAttributeName + "Expression";

            foreach (var attribute in tagHelper.BoundAttributes)
            {
                if (attribute.Name == valueAttributeName)
                {
                    valueAttribute = attribute;
                }

                if (attribute.Name == expressionAttributeName)
                {
                    expressionAttribute = attribute;
                }

                if (valueAttribute != null && expressionAttribute != null)
                {
                    // We found both, so we can stop looking now
                    break;
                }
            }

            if (valueAttribute == null)
            {
                // No matching attribute found.
                continue;
            }

            using var _ = TagHelperDescriptorBuilder.GetPooledInstance(
                TagHelperKind.Bind, tagHelper.Name, tagHelper.AssemblyName,
                out var builder);

            builder.SetTypeName(tagHelper.TypeNameObject);

            builder.DisplayName = tagHelper.DisplayName;
            builder.CaseSensitive = true;
            builder.SetDocumentation(
                DocumentationDescriptor.From(
                    DocumentationId.BindTagHelper_Component,
                    valueAttribute.Name,
                    changeAttribute.Name));

            var metadata = new BindMetadata.Builder
            {
                ValueAttribute = valueAttribute.Name,
                ChangeAttribute = changeAttribute.Name
            };

            if (expressionAttribute != null)
            {
                metadata.ExpressionAttribute = expressionAttribute.Name;
            }

            // Match the component and attribute name
            builder.TagMatchingRule(rule =>
            {
                rule.TagName = tagHelper.TagMatchingRules.Single().TagName;
                rule.Attribute(attribute =>
                {
                    attribute.Name = "@bind-" + valueAttribute.Name;
                    attribute.NameComparison = RequiredAttributeNameComparison.FullMatch;
                    attribute.IsDirectiveAttribute = true;
                });
            });

            builder.TagMatchingRule(rule =>
            {
                rule.TagName = tagHelper.TagMatchingRules.Single().TagName;
                rule.Attribute(attribute =>
                {
                    attribute.Name = "@bind-" + valueAttribute.Name + ":get";
                    attribute.NameComparison = RequiredAttributeNameComparison.FullMatch;
                    attribute.IsDirectiveAttribute = true;
                });
                rule.Attribute(attribute =>
                {
                    attribute.Name = "@bind-" + valueAttribute.Name + ":set";
                    attribute.NameComparison = RequiredAttributeNameComparison.FullMatch;
                    attribute.IsDirectiveAttribute = true;
                });
            });

            builder.BindAttribute(attribute =>
            {
                attribute.SetDocumentation(
                    DocumentationDescriptor.From(
                        DocumentationId.BindTagHelper_Component,
                        valueAttribute.Name,
                        changeAttribute.Name));

                attribute.Name = "@bind-" + valueAttribute.Name;
                attribute.TypeName = changeAttribute.TypeName;
                attribute.IsEnum = valueAttribute.IsEnum;
                attribute.ContainingType = valueAttribute.ContainingType;
                attribute.IsDirectiveAttribute = true;
                attribute.PropertyName = valueAttribute.PropertyName;

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

            if (tagHelper.IsFullyQualifiedNameMatch)
            {
                builder.IsFullyQualifiedNameMatch = true;
            }

            builder.SetMetadata(metadata.Build());

            results.Add(builder.Build());
        }
    }
}
