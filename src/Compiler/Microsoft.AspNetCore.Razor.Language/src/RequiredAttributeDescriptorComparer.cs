﻿// Licensed to the .NET Foundation under one or more agreements.
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
    public static readonly RequiredAttributeDescriptorComparer Default =
        new RequiredAttributeDescriptorComparer();

    private RequiredAttributeDescriptorComparer()
    {
    }

    /// <inheritdoc />
    public bool Equals(
        RequiredAttributeDescriptor? descriptorX,
        RequiredAttributeDescriptor? descriptorY)
    {
        if (object.ReferenceEquals(descriptorX, descriptorY))
        {
            return true;
        }

        if (descriptorX is null)
        {
            return descriptorY is null;
        }
        else if (descriptorY is null)
        {
            return false;
        }

        return
            descriptorX.CaseSensitive == descriptorY.CaseSensitive &&
            descriptorX.NameComparison == descriptorY.NameComparison &&
            descriptorX.ValueComparison == descriptorY.ValueComparison &&
            string.Equals(descriptorX.Name, descriptorY.Name, StringComparison.Ordinal) &&
            string.Equals(descriptorX.Value, descriptorY.Value, StringComparison.Ordinal) &&
            string.Equals(descriptorX.DisplayName, descriptorY.DisplayName, StringComparison.Ordinal);
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
