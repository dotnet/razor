// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language;

internal sealed class TagMatchingRuleDescriptorComparer : IEqualityComparer<TagMatchingRuleDescriptor?>
{
    /// <summary>
    /// A default instance of the <see cref="TagMatchingRuleDescriptorComparer"/>.
    /// </summary>
    public static readonly TagMatchingRuleDescriptorComparer Default = new();

    private TagMatchingRuleDescriptorComparer()
    {
    }

    public bool Equals(TagMatchingRuleDescriptor? x, TagMatchingRuleDescriptor? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null)
        {
            return y is null;
        }
        else if (y is null)
        {
            return false;
        }

        return x.TagName == y.TagName &&
               x.ParentTag == y.ParentTag &&
               x.CaseSensitive == y.CaseSensitive &&
               x.TagStructure == y.TagStructure &&
               x.Attributes.SequenceEqual(y.Attributes, RequiredAttributeDescriptorComparer.Default);
    }

    public int GetHashCode(TagMatchingRuleDescriptor? rule)
    {
        if (rule == null)
        {
            return 0;
        }

        var hash = HashCodeCombiner.Start();
        hash.Add(rule.TagName, StringComparer.Ordinal);

        return hash.CombinedHash;
    }
}
