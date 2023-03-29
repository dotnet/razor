// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

internal partial class DefaultRequiredAttributeDescriptorBuilder
{
    private sealed class Policy : IPooledObjectPolicy<DefaultRequiredAttributeDescriptorBuilder>
    {
        private const int MaxSize = 32;

        public static Policy Instance = new();

        public DefaultRequiredAttributeDescriptorBuilder Create() => new();

        public bool Return(DefaultRequiredAttributeDescriptorBuilder builder)
        {
            builder._parent = null;

            builder.Name = null;
            builder.NameComparisonMode = default;
            builder.Value = null;
            builder.ValueComparisonMode = default;

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
