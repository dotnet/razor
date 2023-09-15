// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language;

public partial class TagMatchingRuleDescriptorBuilder
{
    private sealed class Policy : TagHelperPooledObjectPolicy<TagMatchingRuleDescriptorBuilder>
    {
        public static readonly Policy Instance = new();

        public override TagMatchingRuleDescriptorBuilder Create() => new();

        public override bool Return(TagMatchingRuleDescriptorBuilder builder)
        {
            builder._parent = null;

            builder.TagName = null;
            builder.ParentTag = null;
            builder.TagStructure = default;

            if (builder._requiredAttributeBuilders is { } requiredAttributeBuilders)
            {
                // Make sure that we return all allowed required attribute builders to their pool.
                foreach (var requiredAttributeBuilder in requiredAttributeBuilders)
                {
                    RequiredAttributeDescriptorBuilder.ReturnInstance(requiredAttributeBuilder);
                }

                ClearList(requiredAttributeBuilders);
            }

            ClearDiagnostics(builder._diagnostics);

            return true;
        }
    }
}
