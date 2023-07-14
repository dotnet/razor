// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal static class ChecksumCache
{
    private static readonly ConditionalWeakTable<object, object> s_cache = new();

    public static bool TryGetValue(object value, [NotNullWhen(true)] out Checksum? checksum)
    {
        if (s_cache.TryGetValue(value, out var result))
        {
            checksum = (Checksum)result;
            return true;
        }

        checksum = null;
        return false;
    }

    public static Checksum GetOrCreate(object value, ConditionalWeakTable<object, object>.CreateValueCallback checksumCreator)
        => (Checksum)s_cache.GetValue(value, checksumCreator);

    public static T GetOrCreate<T>(object value, ConditionalWeakTable<object, object>.CreateValueCallback checksumCreator)
        where T : IChecksummedObject
        => (T)s_cache.GetValue(value, checksumCreator);
}
