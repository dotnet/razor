// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace System.Collections.Generic;

internal static class ReadOnlyListExtensions
{
    public static ImmutableArray<TResult> SelectAsArray<T, TResult>(this IReadOnlyList<T> source, Func<T, TResult> selector)
    {
        return source switch
        {
            [] => ImmutableArray<TResult>.Empty,
            [var item] => ImmutableArray.Create(selector(item)),
            [var item1, var item2] => ImmutableArray.Create(selector(item1), selector(item2)),
            [var item1, var item2, var item3] => ImmutableArray.Create(selector(item1), selector(item2), selector(item3)),
            [var item1, var item2, var item3, var item4] => ImmutableArray.Create(selector(item1), selector(item2), selector(item3), selector(item4)),
            var items => BuildResult(items, selector)
        };

        static ImmutableArray<TResult> BuildResult(IReadOnlyList<T> items, Func<T, TResult> selector)
        {
            using var results = new PooledArrayBuilder<TResult>(capacity: items.Count);

            for (var i = 0; i < items.Count; i++)
            {
                results.Add(selector(items[i]));
            }

            return results.DrainToImmutable();
        }
    }

    public static Enumerable<T> AsEnumerable<T>(this IReadOnlyList<T> list) => new(list);

    public readonly ref struct Enumerable<T>(IReadOnlyList<T> list)
    {
        public Enumerator<T> GetEnumerator() => new(list);
    }

    public ref struct Enumerator<T>(IReadOnlyList<T> list)
    {
        private readonly IReadOnlyList<T> _list = list;
        private int _index = 0;
        private T _current = default!;

        public readonly T Current => _current;

        public bool MoveNext()
        {
            if (_index < _list.Count)
            {
                _current = _list[_index];
                _index++;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            _index = 0;
            _current = default!;
        }
    }
}
