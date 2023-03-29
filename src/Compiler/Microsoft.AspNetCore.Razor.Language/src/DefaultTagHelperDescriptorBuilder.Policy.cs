// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

internal partial class DefaultTagHelperDescriptorBuilder
{
    private sealed class Policy : IPooledObjectPolicy<DefaultTagHelperDescriptorBuilder>
    {
        private const int MaxSize = 32;

        public static Policy Instance = new();

        public DefaultTagHelperDescriptorBuilder Create() => new();

        public bool Return(DefaultTagHelperDescriptorBuilder builder)
        {
            builder._kind = null;
            builder._name = null;
            builder._assemblyName = null;

            builder.DisplayName = null;
            builder.TagOutputHint = null;
            builder.CaseSensitive = false;
            builder.Documentation = null;

            ClearBuilderList(builder._allowedChildTags);
            ClearBuilderList(builder._attributeBuilders);
            ClearBuilderList(builder._tagMatchingRuleBuilders);

            if (builder._diagnostics is { } diagnostics)
            {
                diagnostics.Clear();

                if (diagnostics.Capacity > MaxSize)
                {
                    diagnostics.Capacity = MaxSize;
                }
            }

            builder._metadata.Clear();

            return true;

            static void ClearBuilderList<T>(List<T>? builders)
            {
                if (builders is null)
                {
                    return;
                }

                builders.Clear();

                if (builders.Capacity > MaxSize)
                {
                    builders.Capacity = MaxSize;
                }
            }
        }
    }
}
