// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language;

internal partial class DefaultTagHelperDescriptorBuilder
{
    private sealed class Policy : TagHelperPooledObjectPolicy<DefaultTagHelperDescriptorBuilder>
    {
        public static Policy Instance = new();

        public override DefaultTagHelperDescriptorBuilder Create() => new();

        public override bool Return(DefaultTagHelperDescriptorBuilder builder)
        {
            builder._kind = null;
            builder._name = null;
            builder._assemblyName = null;

            builder.DisplayName = null;
            builder.TagOutputHint = null;
            builder.CaseSensitive = false;
            builder.Documentation = null;

            if (builder._allowedChildTags is { } allowedChildTagBuilders)
            {
                foreach (var allowedChildTagBuilder in allowedChildTagBuilders)
                {
                    DefaultAllowedChildTagDescriptorBuilder.Return(allowedChildTagBuilder);
                }

                ClearList(allowedChildTagBuilders);
            }

            if (builder._attributeBuilders is { } attributeBuilders)
            {
                foreach (var attributeBuilder in attributeBuilders)
                {
                    DefaultBoundAttributeDescriptorBuilder.Return(attributeBuilder);
                }

                ClearList(attributeBuilders);
            }

            if (builder._tagMatchingRuleBuilders is { } tagMatchingRuleBuilders)
            {
                foreach (var tagMatchingRuleBuilder in tagMatchingRuleBuilders)
                {
                    DefaultTagMatchingRuleDescriptorBuilder.Return(tagMatchingRuleBuilder);
                }

                ClearList(tagMatchingRuleBuilders);
            }

            ClearDiagnostics(builder._diagnostics);

            builder._metadata.Clear();

            return true;
        }
    }
}
