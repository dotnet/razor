// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language;

internal sealed class TagHelperDescriptorComparer : IEqualityComparer<TagHelperDescriptor?>
{
    /// <summary>
    /// A default instance of the <see cref="TagHelperDescriptorComparer"/>.
    /// </summary>
    public static readonly TagHelperDescriptorComparer Default = new();

    private TagHelperDescriptorComparer()
    {
    }

    public bool Equals(TagHelperDescriptor? x, TagHelperDescriptor? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return x.Kind == y.Kind &&
               x.AssemblyName == y.AssemblyName &&
               x.Name == y.Name &&
               x.CaseSensitive == y.CaseSensitive &&
               x.DisplayName == y.DisplayName &&
               x.DocumentationObject == y.DocumentationObject &&
               x.TagOutputHint == y.TagOutputHint &&
               x.BoundAttributes.SequenceEqual(y.BoundAttributes, BoundAttributeDescriptorComparer.Default) &&
               x.TagMatchingRules.SequenceEqual(y.TagMatchingRules, TagMatchingRuleDescriptorComparer.Default) &&
               x.AllowedChildTags.SequenceEqual(y.AllowedChildTags, AllowedChildTagDescriptorComparer.Default) &&
               ComparerUtilities.Equals(x.Diagnostics, y.Diagnostics, EqualityComparer<RazorDiagnostic>.Default) &&
               x.Metadata.Equals(y.Metadata);
    }

    /// <inheritdoc />
    public int GetHashCode(TagHelperDescriptor? descriptor)
    {
        if (descriptor == null)
        {
            return 0;
        }

        var hash = HashCodeCombiner.Start();
        hash.Add(descriptor.Kind, StringComparer.Ordinal);
        hash.Add(descriptor.AssemblyName, StringComparer.Ordinal);
        hash.Add(descriptor.Name, StringComparer.Ordinal);

        return hash.CombinedHash;
    }
}
