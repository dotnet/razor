// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

internal partial class DefaultBoundAttributeDescriptorBuilder
{
    private sealed class Policy : IPooledObjectPolicy<DefaultBoundAttributeDescriptorBuilder>
    {
        private const int MaxSize = 32;

        public static Policy Instance = new();

        public DefaultBoundAttributeDescriptorBuilder Create() => new();

        public bool Return(DefaultBoundAttributeDescriptorBuilder builder)
        {
            builder._parent = null;
            builder._kind = null;

            builder.Name = null;
            builder.TypeName = null;
            builder.IsEnum = false;
            builder.IsDictionary = false;
            builder.IndexerAttributeNamePrefix = null;
            builder.IndexerValueTypeName = null;
            builder.Documentation = null;
            builder.DisplayName = null;

            if (builder._attributeParameterBuilders is { } attributeParameterBuilders)
            {
                foreach (var attributeParameterBuilder in attributeParameterBuilders)
                {
                    DefaultBoundAttributeParameterDescriptorBuilder.Return(attributeParameterBuilder);
                }

                attributeParameterBuilders.Clear();

                if (attributeParameterBuilders.Capacity > MaxSize)
                {
                    attributeParameterBuilders.Capacity = MaxSize;
                }

            }

            if (builder._diagnostics is { } diagnostics)
            {
                diagnostics.Clear();

                if (diagnostics.Capacity > MaxSize)
                {
                    diagnostics.Capacity = MaxSize;
                }
            }

            builder._metadata?.Clear();

            return true;
        }
    }
}
