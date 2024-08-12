// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.Utilities;

/// <summary>
///  Helper that avoids creating an <see cref="IComparer{T}"/> until its needed.
/// </summary>
internal readonly ref struct CompareHelper<T>
{
    private readonly IComparer<T> _comparer;
    private readonly Comparison<T> _comparison;
    private readonly bool _comparerSpecified;
    private readonly bool _useComparer;
    private readonly bool _descending;

    public CompareHelper(IComparer<T>? comparer, bool descending)
    {
        _comparerSpecified = comparer is not null;
        _comparer = comparer ?? Comparer<T>.Default;
        _useComparer = true;
        _descending = descending;
        _comparison = null!;
    }

    public CompareHelper(Comparison<T> comparison, bool descending)
    {
        _comparison = comparison;
        _useComparer = false;
        _descending = descending;
        _comparer = null!;
    }

    public bool InSortedOrder(T? x, T? y)
    {
        // We assume that x and y are in sorted order if x is > y.
        // We don't consider x == y to be sorted because the actual sor
        // might not be stable, depending on T.

        return _useComparer
            ? !_descending ? _comparer.Compare(x!, y!) > 0 : _comparer.Compare(y!, x!) > 0
            : !_descending ? _comparison(x!, y!) > 0 : _comparison(y!, x!) > 0;
    }

    public IComparer<T> GetOrCreateComparer()
        // There are six cases to consider.
        => (_useComparer, _comparerSpecified, _descending) switch
        {
            // Provided a comparer and the results are in ascending order.
            (true, true, false) => _comparer,

            // Provided a comparer and the results are in descending order.
            (true, true, true) => DescendingComparer<T>.Create(_comparer),

            // Not provided a comparer and the results are in ascending order.
            // In this case, _comparer was already set to Comparer<T>.Default.
            (true, false, false) => _comparer,

            // Not provided a comparer and the results are in descending order.
            (true, false, true) => DescendingComparer<T>.Default,

            // Provided a comparison delegate and the results are in ascending order.
            (false, _, false) => Comparer<T>.Create(_comparison),

            // Provided a comparison delegate and the results are in descending order.
            (false, _, true) => DescendingComparer<T>.Create(_comparison)
        };
}
