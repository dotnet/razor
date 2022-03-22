// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Razor.Language
{
    public static class TestRequiredAttributeDescriptorBuilderExtensions
    {
        public static RequiredAttributeDescriptorBuilder Name(this RequiredAttributeDescriptorBuilder builder!!, string name)
        {
            builder.Name = name;

            return builder;
        }

        public static RequiredAttributeDescriptorBuilder NameComparisonMode(
            this RequiredAttributeDescriptorBuilder builder!!,
            RequiredAttributeDescriptor.NameComparisonMode nameComparison)
        {
            builder.NameComparisonMode = nameComparison;

            return builder;
        }

        public static RequiredAttributeDescriptorBuilder Value(this RequiredAttributeDescriptorBuilder builder!!, string value)
        {
            builder.Value = value;

            return builder;
        }

        public static RequiredAttributeDescriptorBuilder ValueComparisonMode(
            this RequiredAttributeDescriptorBuilder builder!!,
            RequiredAttributeDescriptor.ValueComparisonMode valueComparison)
        {
            builder.ValueComparisonMode = valueComparison;

            return builder;
        }

        public static RequiredAttributeDescriptorBuilder AddDiagnostic(this RequiredAttributeDescriptorBuilder builder, RazorDiagnostic diagnostic)
        {
            builder.Diagnostics.Add(diagnostic);

            return builder;
        }
    }
}
