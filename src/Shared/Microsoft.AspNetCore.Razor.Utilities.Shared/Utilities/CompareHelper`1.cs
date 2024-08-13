// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.AspNetCore.Razor.Utilities;

/// <summary>
///  Helper that avoids creating an <see cref="IComparer{T}"/> until its needed.
/// </summary>
internal readonly ref struct CompareHelper<T>
{
    private readonly IComparer<T> _comparer;
    private readonly Comparison<T>? _comparison;
    private readonly bool _descending;

    public CompareHelper(IComparer<T>? comparer, bool descending)
    {
        _comparer = comparer ?? Comparer<T>.Default;
        _comparison = null;
        _descending = descending;
    }

    public CompareHelper(Comparison<T> comparison, bool descending)
    {
        _comparer = null!; // This value will never be used when _comparison is non-null.
        _comparison = comparison;
        _descending = descending;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool InSortedOrder(T? x, T? y)
    {
        // We assume that x and y are in sorted order if x is > y.
        // We don't consider x == y to be sorted because the actual sort
        // might not be stable, depending on T.

        return _comparison is null
            ? !_descending ? _comparer.Compare(x!, y!) > 0 : _comparer.Compare(y!, x!) > 0
            : !_descending ? _comparison(x!, y!) > 0 : _comparison(y!, x!) > 0;
    }

    public IComparer<T> GetOrCreateComparer()
    {
        return _comparison is null
            ? !_descending ? _comparer : DescendingComparer<T>.Create(_comparer)
            : !_descending ? Comparer<T>.Create(_comparison) : DescendingComparer<T>.Create(_comparison);
    }
}
