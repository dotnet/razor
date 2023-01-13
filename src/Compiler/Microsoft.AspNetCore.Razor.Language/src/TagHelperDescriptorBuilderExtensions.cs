﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;

namespace Microsoft.AspNetCore.Razor.Language;

public static class TagHelperDescriptorBuilderExtensions
{
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

        builder.Metadata[TagHelperMetadata.Common.TypeNamespace] = typeNamespace;
    }

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

        builder.Metadata[TagHelperMetadata.Common.TypeNameIdentifier] = typeNameIdentifier;
    }

    public static void SetTypeName(this TagHelperDescriptorBuilder builder, string typeName)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (typeName == null)
        {
            throw new ArgumentNullException(nameof(typeName));
        }

        builder.Metadata[TagHelperMetadata.Common.TypeName] = typeName;
    }

    public static string GetTypeName(this TagHelperDescriptorBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (builder.Metadata.ContainsKey(TagHelperMetadata.Common.TypeName))
        {
            return builder.Metadata[TagHelperMetadata.Common.TypeName];
        }

        return null;
    }
}
