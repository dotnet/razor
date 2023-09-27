// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language;

/// <summary>
/// An <see cref="IEqualityComparer{TagHelperRequiredAttributeDescriptor}"/> used to check equality between
/// two <see cref="RequiredAttributeDescriptor"/>s.
/// </summary>
internal sealed class RequiredAttributeDescriptorComparer : IEqualityComparer<RequiredAttributeDescriptor?>
{
    /// <summary>
    /// A default instance of the <see cref="RequiredAttributeDescriptorComparer"/>.
    /// </summary>
    public static readonly RequiredAttributeDescriptorComparer Default = new();

    private RequiredAttributeDescriptorComparer()
    {
    }

    /// <inheritdoc />
    public bool Equals(RequiredAttributeDescriptor? x, RequiredAttributeDescriptor? y)
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

        return x.CaseSensitive == y.CaseSensitive &&
               x.NameComparison == y.NameComparison &&
               x.ValueComparison == y.ValueComparison &&
               x.Name == y.Name &&
               x.Value == y.Value &&
               x.DisplayName == y.DisplayName;
    }

    /// <inheritdoc />
    public int GetHashCode(RequiredAttributeDescriptor? descriptor)
    {
        if (descriptor == null)
        {
            return 0;
        }

        var hash = HashCodeCombiner.Start();
        hash.Add(descriptor.Name, StringComparer.Ordinal);

        return hash.CombinedHash;
    }
}
