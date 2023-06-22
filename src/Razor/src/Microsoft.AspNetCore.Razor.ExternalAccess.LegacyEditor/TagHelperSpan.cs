// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.LegacyEditor;

internal readonly record struct TagHelperSpan(TagHelperBinding Binding, RazorSourceSpan Span)
{
    public TagHelperSpan(RazorSourceSpan span, TagHelperBinding binding)
        : this(binding ?? throw new ArgumentNullException(nameof(binding)), span)
    {
    }

    public IEnumerable<TagHelperDescriptor> TagHelpers => Binding.Descriptors;
}
