// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.AspNetCore.Razor.Language;

public static class TagHelperDescriptorBuilderExtensions
{
    public static void SetMetadata(
        this TagHelperDescriptorBuilder builder,
        KeyValuePair<string, string> pair)
    {
        builder.SetMetadata(MetadataCollection.Create(pair));
    }

    public static void SetMetadata(
        this TagHelperDescriptorBuilder builder,
        KeyValuePair<string, string> pair1,
        KeyValuePair<string, string> pair2)
    {
        builder.SetMetadata(MetadataCollection.Create(pair1, pair2));
    }

    internal static void SetMetadata(
        this TagHelperDescriptorBuilder builder,
        KeyValuePair<string, string> pair1,
        KeyValuePair<string, string> pair2,
        KeyValuePair<string, string> pair3)
    {
        builder.SetMetadata(MetadataCollection.Create(pair1, pair2, pair3));
    }

    internal static void SetMetadata(
        this TagHelperDescriptorBuilder builder,
        params KeyValuePair<string, string>[] pairs)
    {
        builder.SetMetadata(MetadataCollection.Create(pairs));
    }

    [Obsolete($"Do not use this method. {nameof(TagHelperDescriptorBuilder.SetMetadata)} should be used instead.")]
    public static void SetTypeNamespace(this TagHelperDescriptorBuilder builder, string typeNamespace)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (typeNamespace == null)
        {
            throw new ArgumentNullException(nameof(typeNamespace));
        }

        Debug.Fail($"Do not use this method. {nameof(TagHelperDescriptorBuilder.SetMetadata)} should be used instead.");

        builder.Metadata[TagHelperMetadata.Common.TypeNamespace] = typeNamespace;
    }

    [Obsolete($"Do not use this method. {nameof(TagHelperDescriptorBuilder.SetMetadata)} should be used instead.")]
    public static void SetTypeNameIdentifier(this TagHelperDescriptorBuilder builder, string typeNameIdentifier)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (typeNameIdentifier == null)
        {
            throw new ArgumentNullException(nameof(typeNameIdentifier));
        }

        Debug.Fail($"Do not use this method. {nameof(TagHelperDescriptorBuilder.SetMetadata)} should be used instead.");

        builder.Metadata[TagHelperMetadata.Common.TypeNameIdentifier] = typeNameIdentifier;
    }
}
