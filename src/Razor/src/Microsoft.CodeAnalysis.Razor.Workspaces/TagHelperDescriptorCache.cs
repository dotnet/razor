// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor
{
    internal static class TagHelperDescriptorCache
    {
        private static readonly MemoryCache<int, TagHelperDescriptor> s_cachedTagHelperDescriptors =
            new MemoryCache<int, TagHelperDescriptor>(4500);

        internal static bool TryGetDescriptor(int hashCode, out TagHelperDescriptor descriptor) =>
            s_cachedTagHelperDescriptors.TryGetValue(hashCode, out descriptor);

        internal static void Set(int hashCode, TagHelperDescriptor descriptor) =>
            s_cachedTagHelperDescriptors.Set(hashCode, descriptor);
    }
}
