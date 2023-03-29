// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

internal partial class DefaultAllowedChildTagDescriptorBuilder
{
    private sealed class Policy : IPooledObjectPolicy<DefaultAllowedChildTagDescriptorBuilder>
    {
        private const int MaxSize = 32;

        public static Policy Instance = new();

        public DefaultAllowedChildTagDescriptorBuilder Create() => new();

        public bool Return(DefaultAllowedChildTagDescriptorBuilder builder)
        {
            builder._parent = null;

            builder.Name = null;
            builder.DisplayName = null;

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
