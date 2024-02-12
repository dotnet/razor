// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;

namespace Microsoft.AspNetCore.Razor.Language;

/// <summary>
/// The binding information for Tag Helpers resulted to a <see cref="RazorCodeDocument"/>. Represents the
/// Tag Helper information after processing by directives.
/// </summary>
internal sealed class TagHelperDocumentContext
{
    public string? Prefix { get; }
    public ImmutableArray<TagHelperDescriptor> TagHelpers { get; }

    private TagHelperDocumentContext(string? prefix, ImmutableArray<TagHelperDescriptor> tagHelpers)
    {
        Prefix = prefix;
        TagHelpers = tagHelpers;
    }

    public static TagHelperDocumentContext Create(string? prefix, ImmutableArray<TagHelperDescriptor> tagHelpers)
    {
        if (tagHelpers.IsDefault)
        {
            throw new ArgumentNullException(nameof(tagHelpers));
        }

        return new(prefix, tagHelpers);
    }
}
