// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.LegacyEditor;

internal static partial class RazorWrapperFactory
{
    private class TagHelperDescriptorWrapper(TagHelperDescriptor obj) : Wrapper<TagHelperDescriptor>(obj), IRazorTagHelperDescriptor
    {
        private ImmutableArray<IRazorBoundAttributeDescriptor> _boundAttributes;
        private ImmutableArray<IRazorTagMatchingRuleDescriptor> _tagMatchingRules;

        public string DisplayName => Object.DisplayName;
        public string? Documentation => Object.Documentation;
        public bool CaseSensitive => Object.CaseSensitive;
        public string? TagOutputHint => Object.TagOutputHint;

        public ImmutableArray<IRazorBoundAttributeDescriptor> BoundAttributes
        {
            get
            {
                var result = _boundAttributes;
                return result.IsDefault
                    ? InterlockedOperations.Initialize(ref _boundAttributes, WrapAll(Object.BoundAttributes, Wrap))
                    : result;
            }
        }

        public ImmutableArray<IRazorTagMatchingRuleDescriptor> TagMatchingRules
        {
            get
            {
                var result = _tagMatchingRules;
                return result.IsDefault
                    ? InterlockedOperations.Initialize(ref _tagMatchingRules, WrapAll(Object.TagMatchingRules, Wrap))
                    : result;
            }
        }

        public override bool Equals(object obj)
            => obj is IRazorTagHelperDescriptor other &&
               Equals(other);

        public bool Equals(IRazorTagHelperDescriptor other)
            => other is not null &&
               Object.Equals(Unwrap(other));

        public override int GetHashCode()
            => Object.GetHashCode();

        public bool IsComponentOrChildContentTagHelper()
            => Object.IsComponentOrChildContentTagHelper();
    }
}
