// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class TagHelperDescriptorProviderContext
{
    public bool ExcludeHidden { get; set; }
    public bool IncludeDocumentation { get; set; }

    public ItemCollection Items { get; }
    public ICollection<TagHelperDescriptor> Results { get; }

    private TagHelperDescriptorProviderContext(ICollection<TagHelperDescriptor> results)
    {
        Results = results;
        Items = [];
    }

    public static TagHelperDescriptorProviderContext Create()
        => new([]);

    public static TagHelperDescriptorProviderContext Create(ICollection<TagHelperDescriptor> results)
        => new(results);
}
