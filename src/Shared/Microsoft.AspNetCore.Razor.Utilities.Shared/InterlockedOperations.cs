// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Microsoft.AspNetCore.Razor;

internal static class InterlockedOperations
{
    public static T Initialize<T>([NotNull] ref T? target, T value)
        where T : class
        => Interlocked.CompareExchange(ref target, value, null) ?? value;

    public static ImmutableArray<T> Initialize<T>(ref ImmutableArray<T> target, ImmutableArray<T> initializedValue)
    {
        Debug.Assert(!initializedValue.IsDefault);
        var oldValue = ImmutableInterlocked.InterlockedCompareExchange(ref target, initializedValue, default(ImmutableArray<T>));
        return oldValue.IsDefault ? initializedValue : oldValue;
    }
}
