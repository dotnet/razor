// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Language;

internal sealed class BoundAttributeDescriptorComparer : IEqualityComparer<BoundAttributeDescriptor?>
{
    /// <summary>
    /// A default instance of the <see cref="BoundAttributeDescriptorComparer"/>.
    /// </summary>
    public static readonly BoundAttributeDescriptorComparer Default = new();

    private BoundAttributeDescriptorComparer()
    {
    }

    public bool Equals(BoundAttributeDescriptor? x, BoundAttributeDescriptor? y)
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
               x.IsIndexerStringProperty == y.IsIndexerStringProperty &&
               x.IsEnum == y.IsEnum &&
               x.HasIndexer == y.HasIndexer &&
               x.CaseSensitive == y.CaseSensitive &&
               x.IsEditorRequired == y.IsEditorRequired &&
               x.Name == y.Name &&
               x.IndexerNamePrefix == y.IndexerNamePrefix &&
               x.TypeName == y.TypeName &&
               x.IndexerTypeName == y.IndexerTypeName &&
               x.DocumentationObject == y.DocumentationObject &&
               x.DisplayName == y.DisplayName &&
               x.Parameters.SequenceEqual(y.Parameters, BoundAttributeParameterDescriptorComparer.Default) &&
               x.Metadata.Equals(y.Metadata);
    }

    public int GetHashCode(BoundAttributeDescriptor? descriptor)
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
