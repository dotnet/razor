// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Razor.Language;

public static class TestRequiredAttributeDescriptorBuilderExtensions
{
    public static RequiredAttributeDescriptorBuilder Name(this RequiredAttributeDescriptorBuilder builder, string name)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Name = name;

        return builder;
    }

    public static RequiredAttributeDescriptorBuilder NameComparison(
        this RequiredAttributeDescriptorBuilder builder,
        RequiredAttributeNameComparison nameComparison)
    {
        builder.NameComparison = nameComparison;

        return builder;
    }

    public static RequiredAttributeDescriptorBuilder Value(this RequiredAttributeDescriptorBuilder builder, string value)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Value = value;

        return builder;
    }

    public static RequiredAttributeDescriptorBuilder ValueComparison(
        this RequiredAttributeDescriptorBuilder builder,
        RequiredAttributeValueComparison valueComparison)
    {
        builder.ValueComparison = valueComparison;

        return builder;
    }

    public static RequiredAttributeDescriptorBuilder AddDiagnostic(this RequiredAttributeDescriptorBuilder builder, RazorDiagnostic diagnostic)
    {
        builder.Diagnostics.Add(diagnostic);

        return builder;
    }
}
