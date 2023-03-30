// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language;

internal partial class DefaultTagMatchingRuleDescriptorBuilder
{
    private sealed class Policy : TagHelperPooledObjectPolicy<DefaultTagMatchingRuleDescriptorBuilder>
    {
        public static readonly Policy Instance = new();

        public override DefaultTagMatchingRuleDescriptorBuilder Create() => new();

        public override bool Return(DefaultTagMatchingRuleDescriptorBuilder builder)
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

                ClearList(requiredAttributeBuilders);
            }

            ClearDiagnostics(builder._diagnostics);

            return true;
        }
    }
}
