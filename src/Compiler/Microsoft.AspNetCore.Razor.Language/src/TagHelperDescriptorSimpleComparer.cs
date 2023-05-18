// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language;

/// <summary>
/// Considers two descriptors equal if they are definitions of the same class.
/// </summary>
internal sealed class TagHelperDescriptorSimpleComparer : IEqualityComparer<TagHelperDescriptor>
{
    public static readonly TagHelperDescriptorSimpleComparer Default = new();

    private TagHelperDescriptorSimpleComparer() { }

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
            x.IsComponentFullyQualifiedNameMatch() == y.IsComponentFullyQualifiedNameMatch();
    }

    public int GetHashCode(TagHelperDescriptor obj)
    {
        var hash = HashCodeCombiner.Start();
        hash.Add(obj.Kind, StringComparer.Ordinal);
        hash.Add(obj.AssemblyName, StringComparer.Ordinal);
        hash.Add(obj.Name, StringComparer.Ordinal);
        return hash.CombinedHash;
    }
}
