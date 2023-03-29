// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

internal partial class DefaultBoundAttributeParameterDescriptorBuilder
{
    private sealed class Policy : IPooledObjectPolicy<DefaultBoundAttributeParameterDescriptorBuilder>
    {
        private const int MaxSize = 32;

        public static Policy Instance = new();

        public DefaultBoundAttributeParameterDescriptorBuilder Create() => new();

        public bool Return(DefaultBoundAttributeParameterDescriptorBuilder builder)
        {
            builder._parent = null;
            builder._kind = null;

            builder.Name = null;
            builder.TypeName = null;
            builder.IsEnum = false;
            builder.Documentation = null;
            builder.DisplayName = null;

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
