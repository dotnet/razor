// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal static class TagHelperDescriptorCache
{
    private static readonly MemoryCache<Checksum, TagHelperDescriptor> s_cachedTagHelperDescriptors = new(sizeLimit: 4500);

    public static bool TryGetDescriptor(Checksum checksum, [NotNullWhen(true)] out TagHelperDescriptor? descriptor) =>
        s_cachedTagHelperDescriptors.TryGetValue(checksum, out descriptor);

    public static void Set(Checksum checksum, TagHelperDescriptor descriptor) =>
        s_cachedTagHelperDescriptors.Set(checksum, descriptor);
}
