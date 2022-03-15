// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Razor.Language
{
    public static class TestTagHelperDescriptorBuilderExtensions
    {
        public static TagHelperDescriptorBuilder TypeName(this TagHelperDescriptorBuilder builder!!, string typeName)
        {
            builder.SetTypeName(typeName);

            return builder;
        }

        public static TagHelperDescriptorBuilder DisplayName(this TagHelperDescriptorBuilder builder!!, string displayName)
        {
            builder.DisplayName = displayName;

            return builder;
        }

        public static TagHelperDescriptorBuilder AllowChildTag(this TagHelperDescriptorBuilder builder!!, string allowedChild)
        {
            builder.AllowChildTag(childTagBuilder => childTagBuilder.Name = allowedChild);

            return builder;
        }

        public static TagHelperDescriptorBuilder TagOutputHint(this TagHelperDescriptorBuilder builder!!, string hint)
        {
            builder.TagOutputHint = hint;

            return builder;
        }

        public static TagHelperDescriptorBuilder SetCaseSensitive(this TagHelperDescriptorBuilder builder!!)
        {
            builder.CaseSensitive = true;

            return builder;
        }

        public static TagHelperDescriptorBuilder Documentation(this TagHelperDescriptorBuilder builder!!, string documentation)
        {
            builder.Documentation = documentation;

            return builder;
        }

        public static TagHelperDescriptorBuilder AddMetadata(this TagHelperDescriptorBuilder builder!!, string key, string value)
        {
            builder.Metadata[key] = value;

            return builder;
        }

        public static TagHelperDescriptorBuilder AddDiagnostic(this TagHelperDescriptorBuilder builder!!, RazorDiagnostic diagnostic)
        {
            builder.Diagnostics.Add(diagnostic);

            return builder;
        }

        public static TagHelperDescriptorBuilder BoundAttributeDescriptor(
            this TagHelperDescriptorBuilder builder!!,
            Action<BoundAttributeDescriptorBuilder> configure)
        {
            builder.BindAttribute(configure);

            return builder;
        }

        public static TagHelperDescriptorBuilder TagMatchingRuleDescriptor(
            this TagHelperDescriptorBuilder builder!!,
            Action<TagMatchingRuleDescriptorBuilder> configure)
        {
            builder.TagMatchingRule(configure);

            return builder;
        }
    }
}
