// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class TagHelperDescriptorProviderContext
{
    public Compilation Compilation { get; }

    public bool ExcludeHidden { get; set; }
    public bool IncludeDocumentation { get; set; }

    public ItemCollection Items { get; }
    public ICollection<TagHelperDescriptor> Results { get; }

    private TagHelperDescriptorProviderContext(Compilation compilation, ICollection<TagHelperDescriptor> results)
    {
        Compilation = compilation;
        Results = results;
        Items = [];
    }

    public static TagHelperDescriptorProviderContext Create(Compilation compilation)
        => new(compilation, results: []);

    public static TagHelperDescriptorProviderContext Create(Compilation compilation, ICollection<TagHelperDescriptor> results)
        => new(compilation, results);
}
