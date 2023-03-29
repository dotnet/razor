// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

internal partial class DefaultTagMatchingRuleDescriptorBuilder
{
    private sealed class Policy : IPooledObjectPolicy<DefaultTagMatchingRuleDescriptorBuilder>
    {
        private const int MaxSize = 32;

        public static Policy Instance = new();

        public DefaultTagMatchingRuleDescriptorBuilder Create() => new();

        public bool Return(DefaultTagMatchingRuleDescriptorBuilder builder)
        {
            builder._parent = null;

            builder.TagName = null;
            builder.ParentTag = null;
            builder.TagStructure = default;

            if (builder._requiredAttributeBuilders is { } requiredAttributeBuilders)
            {
                foreach (var requiredAttributeBuilder in requiredAttributeBuilders)
                {
                    DefaultRequiredAttributeDescriptorBuilder.Return(requiredAttributeBuilder);
                }

                requiredAttributeBuilders.Clear();

                if (requiredAttributeBuilders.Capacity > MaxSize)
                {
                    requiredAttributeBuilders.Capacity = MaxSize;
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

            return true;
        }
    }
}
