// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.LegacyEditor;

internal static partial class RazorWrapperFactory
{
    private class TagHelperBindingWrapper(TagHelperBinding obj) : Wrapper<TagHelperBinding>(obj), IRazorTagHelperBinding
    {
        private ImmutableArray<IRazorTagHelperDescriptor> _descriptors;

        public ImmutableArray<IRazorTagHelperDescriptor> Descriptors
        {
            get
            {
                var result = _descriptors;
                return result.IsDefault
                    ? InterlockedOperations.Initialize(ref _descriptors, WrapAll(Object.Descriptors, Wrap))
                    : result;
            }
        }

        public ImmutableArray<IRazorTagMatchingRuleDescriptor> GetBoundRules(IRazorTagHelperDescriptor descriptor)
        {
            return WrapAll(Object.GetBoundRules(Unwrap(descriptor)), Wrap);
        }
    }
}
