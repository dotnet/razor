// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Razor.Language
{
    public static class TestBoundAttributeDescriptorBuilderExtensions
    {
        public static BoundAttributeDescriptorBuilder Name(this BoundAttributeDescriptorBuilder builder!!, string name)
        {
            builder.Name = name;

            return builder;
        }

        public static BoundAttributeDescriptorBuilder TypeName(this BoundAttributeDescriptorBuilder builder!!, string typeName)
        {
            builder.TypeName = typeName;

            return builder;
        }

        public static BoundAttributeDescriptorBuilder PropertyName(this BoundAttributeDescriptorBuilder builder!!, string propertyName)
        {
            builder.SetPropertyName(propertyName);

            return builder;
        }

        public static BoundAttributeDescriptorBuilder DisplayName(this BoundAttributeDescriptorBuilder builder!!, string displayName)
        {
            builder.DisplayName = displayName;

            return builder;
        }

        public static BoundAttributeDescriptorBuilder AsEnum(this BoundAttributeDescriptorBuilder builder!!)
        {
            builder.IsEnum = true;

            return builder;
        }

        public static BoundAttributeDescriptorBuilder AsDictionaryAttribute(
            this BoundAttributeDescriptorBuilder builder!!,
            string attributeNamePrefix,
            string valueTypeName)
        {
            builder.IsDictionary = true;
            builder.IndexerAttributeNamePrefix = attributeNamePrefix;
            builder.IndexerValueTypeName = valueTypeName;

            return builder;
        }

        public static BoundAttributeDescriptorBuilder Documentation(this BoundAttributeDescriptorBuilder builder!!, string documentation)
        {
            builder.Documentation = documentation;

            return builder;
        }

        public static BoundAttributeDescriptorBuilder AddMetadata(this BoundAttributeDescriptorBuilder builder!!, string key, string value)
        {
            builder.Metadata[key] = value;

            return builder;
        }

        public static BoundAttributeDescriptorBuilder AddDiagnostic(this BoundAttributeDescriptorBuilder builder!!, RazorDiagnostic diagnostic)
        {
            builder.Diagnostics.Add(diagnostic);

            return builder;
        }
    }
}
