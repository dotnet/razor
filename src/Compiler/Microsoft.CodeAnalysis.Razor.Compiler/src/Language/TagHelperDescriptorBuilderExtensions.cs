// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Language;

public static class TagHelperDescriptorBuilderExtensions
{
    public static void SetMetadata(
        this TagHelperDescriptorBuilder builder,
        KeyValuePair<string, string?> pair)
    {
        builder.SetMetadata(MetadataCollection.Create(pair));
    }

    public static void SetMetadata(
        this TagHelperDescriptorBuilder builder,
        KeyValuePair<string, string?> pair1,
        KeyValuePair<string, string?> pair2)
    {
        builder.SetMetadata(MetadataCollection.Create(pair1, pair2));
    }

    internal static void SetMetadata(
        this TagHelperDescriptorBuilder builder,
        KeyValuePair<string, string?> pair1,
        KeyValuePair<string, string?> pair2,
        KeyValuePair<string, string?> pair3)
    {
        builder.SetMetadata(MetadataCollection.Create(pair1, pair2, pair3));
    }

    internal static void SetMetadata(
        this TagHelperDescriptorBuilder builder,
        params KeyValuePair<string, string?>[] pairs)
    {
        builder.SetMetadata(MetadataCollection.Create(pairs));
    }
}
