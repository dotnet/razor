// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language;

public partial class BoundAttributeDescriptorBuilder
{
    private sealed class Policy : TagHelperPooledObjectPolicy<BoundAttributeDescriptorBuilder>
    {
        public static readonly Policy Instance = new();

        public override BoundAttributeDescriptorBuilder Create() => new();

        public override bool Return(BoundAttributeDescriptorBuilder builder)
        {
            builder._parent = null;
            builder._kind = null;
            builder._documentationObject = default;

            builder.Name = null;
            builder.TypeName = null;
            builder.IsEnum = false;
            builder.IsDictionary = false;
            builder.IsEditorRequired = false;
            builder.IndexerAttributeNamePrefix = null;
            builder.IndexerValueTypeName = null;
            builder.DisplayName = null;

            if (builder._attributeParameterBuilders is { } attributeParameterBuilders)
            {
                // Make sure that we return all parameter builders to their pool.
                foreach (var attributeParameterBuilder in attributeParameterBuilders)
                {
                    BoundAttributeParameterDescriptorBuilder.ReturnInstance(attributeParameterBuilder);
                }

                ClearList(attributeParameterBuilders);
            }

            builder._diagnostics?.Clear();
            builder._metadata.Clear();

            return true;
        }
    }
}
