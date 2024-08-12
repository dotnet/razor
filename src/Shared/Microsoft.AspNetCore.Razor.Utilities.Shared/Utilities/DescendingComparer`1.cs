// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Utilities;

internal abstract class DescendingComparer<T> : IComparer<T>
{
    private static IComparer<T>? s_default;

    public static IComparer<T> Default => s_default ??= new ReversedComparer(Comparer<T>.Default);

    public static IComparer<T> Create(IComparer<T> comparer)
        => new ReversedComparer(comparer);

    public static IComparer<T> Create(Comparison<T> comparison)
        => new ReversedComparison(comparison);

    public abstract int Compare(T? x, T? y);

    private sealed class ReversedComparer(IComparer<T> comparer) : DescendingComparer<T>
    {
        public override int Compare(T? x, T? y)
            => comparer.Compare(y!, x!);
    }

    private sealed class ReversedComparison(Comparison<T> comparison) : DescendingComparer<T>
    {
        public override int Compare(T? x, T? y)
            => comparison(y!, x!);
    }
}
