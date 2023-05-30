// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Language.Components;

namespace Microsoft.AspNetCore.Razor.Language;

public static class RequiredAttributeDescriptorBuilderExtensions
{
    [Obsolete($"Do not use this method. {nameof(RequiredAttributeDescriptorBuilder.TryGetMetadataValue)} should be used instead.")]
    internal static bool IsDirectiveAttribute(this RequiredAttributeDescriptorBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        Debug.Fail($"Do not use this method. {nameof(RequiredAttributeDescriptorBuilder.TryGetMetadataValue)} should be used instead.");

        return builder.TryGetMetadataValue(ComponentMetadata.Common.DirectiveAttribute, out var value) &&
               value == bool.TrueString;
    }
}
