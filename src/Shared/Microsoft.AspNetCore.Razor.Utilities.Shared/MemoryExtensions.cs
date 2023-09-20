// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;

#if !NET8_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace System;

internal static class MemoryExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void Replace(this ReadOnlySpan<char> source, Span<char> destination, char oldValue, char newValue)
    {
#if NET8_0_OR_GREATER
        source.Replace(destination, oldValue, newValue);
#else
        var length = source.Length;
        if (length == 0)
        {
            return;
        }

        if (length > destination.Length)
        {
            throw new ArgumentException(SR.Destination_is_too_short, nameof(destination));
        }

        ref var src = ref MemoryMarshal.GetReference(source);
        ref var dst = ref MemoryMarshal.GetReference(destination);

        for (var i = 0; i < length; i++)
        {
            var original = Unsafe.Add(ref src, i);
            Unsafe.Add(ref dst, i) = original == oldValue ? newValue : original;
        }
#endif
    }
}
