// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language;

internal sealed class BoundAttributeParameterDescriptorComparer : IEqualityComparer<BoundAttributeParameterDescriptor?>
{
    /// <summary>
    /// A default instance of the <see cref="BoundAttributeParameterDescriptorComparer"/>.
    /// </summary>
    public static readonly BoundAttributeParameterDescriptorComparer Default = new();

    private BoundAttributeParameterDescriptorComparer()
    {
    }

    public bool Equals(BoundAttributeParameterDescriptor? x, BoundAttributeParameterDescriptor? y)
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

        return x.Kind == y.Kind &&
               x.IsEnum == y.IsEnum &&
               x.Name == y.Name &&
               x.TypeName == y.TypeName &&
               x.DocumentationObject == y.DocumentationObject &&
               x.DisplayName == y.DisplayName &&
               x.Metadata.Equals(y.Metadata);
    }

    public int GetHashCode(BoundAttributeParameterDescriptor? descriptor)
    {
        if (descriptor == null)
        {
            return 0;
        }

        var hash = HashCodeCombiner.Start();
        hash.Add(descriptor.Kind, StringComparer.Ordinal);
        hash.Add(descriptor.Name, StringComparer.Ordinal);

        return hash.CombinedHash;
    }
}
