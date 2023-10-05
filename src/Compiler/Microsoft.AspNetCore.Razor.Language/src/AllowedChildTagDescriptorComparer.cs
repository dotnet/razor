// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language;

internal sealed class AllowedChildTagDescriptorComparer : IEqualityComparer<AllowedChildTagDescriptor?>
{
    /// <summary>
    /// A default instance of the <see cref="AllowedChildTagDescriptorComparer"/>.
    /// </summary>
    public static readonly AllowedChildTagDescriptorComparer Default = new();

    private AllowedChildTagDescriptorComparer()
    {
    }

    /// <inheritdoc />
    public bool Equals(AllowedChildTagDescriptor? x, AllowedChildTagDescriptor? y)
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

        return x.Name == y.Name &&
               x.DisplayName == y.DisplayName;
    }

    /// <inheritdoc />
    public int GetHashCode(AllowedChildTagDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return 0;
        }

        var hash = HashCodeCombiner.Start();
        hash.Add(descriptor.Name, StringComparer.Ordinal);

        return hash.CombinedHash;
    }
}
