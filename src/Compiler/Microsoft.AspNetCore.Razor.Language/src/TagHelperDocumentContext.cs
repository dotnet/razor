// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Language;

/// <summary>
/// The binding information for Tag Helpers resulted to a <see cref="RazorCodeDocument"/>. Represents the
/// Tag Helper information after processing by directives.
/// </summary>
internal sealed class TagHelperDocumentContext
{
    public string? Prefix { get; }
    public IReadOnlyList<TagHelperDescriptor> TagHelpers { get; }

    private TagHelperDocumentContext(string? prefix, IReadOnlyList<TagHelperDescriptor> tagHelpers)
    {
        Prefix = prefix;
        TagHelpers = tagHelpers;
    }

    public static TagHelperDocumentContext Create(string? prefix, IEnumerable<TagHelperDescriptor> tagHelpers)
    {
        if (tagHelpers == null)
        {
            throw new ArgumentNullException(nameof(tagHelpers));
        }

        return new(prefix, tagHelpers.ToArray());
    }

    internal static TagHelperDocumentContext Create(string? prefix, IReadOnlyList<TagHelperDescriptor> tagHelpers)
    {
        if (tagHelpers == null)
        {
            throw new ArgumentNullException(nameof(tagHelpers));
        }

        return new(prefix, tagHelpers);
    }
}
