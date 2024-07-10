// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Utilities.Shared.Test;

public class ImmutableArrayExtensionsTests
{
    [Fact]
    public void GetMostRecentUniqueItems()
    {
        ImmutableArray<string> items =
        [
            "Hello",
            "HELLO",
            "HeLlO",
            new string([',', ' ']),
            new string([',', ' ']),
            "World",
            "WORLD",
            "WoRlD"
        ];

        var mostRecent = items.GetMostRecentUniqueItems(StringComparer.OrdinalIgnoreCase);

        Assert.Collection(mostRecent,
            s => Assert.Equal("HeLlO", s),
            s =>
            {
                // make sure it's the most recent ", "
                Assert.NotSame(items[3], s);
                Assert.Same(items[4], s);
            },
            s => Assert.Equal("WoRlD", s));
    }

    public static TheoryData<ImmutableArray<int>, ImmutableArray<int>> OrderAsArrayData
    {
        get
        {
            return new()
            {
                { [1, 2, 3, 4, 5, 6, 7, 8, 9, 10], [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] },
                { [10, 9, 8, 7, 6, 5, 4, 3, 2, 1], [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] },
                { [1, 3, 5, 7, 9, 2, 4, 6, 8, 10], [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] },
                { [2, 5, 8, 1, 3, 9, 7, 4, 10, 6], [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] },
                { [6, 10, 4, 7, 9, 3, 1, 8, 5, 2], [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] },
            };
        }
    }

    [Theory]
    [MemberData(nameof(OrderAsArrayData))]
    public void OrderAsArray(ImmutableArray<int> data, ImmutableArray<int> expected)
    {
        var sorted = data.OrderAsArray();
        Assert.Equal<int>(expected, sorted);
    }

    public static TheoryData<ImmutableArray<int>, ImmutableArray<int>> OrderDescendingAsArrayData
    {
        get
        {
            return new()
            {
                { [1, 2, 3, 4, 5, 6, 7, 8, 9, 10], [10, 9, 8, 7, 6, 5, 4, 3, 2, 1] },
                { [10, 9, 8, 7, 6, 5, 4, 3, 2, 1], [10, 9, 8, 7, 6, 5, 4, 3, 2, 1] },
                { [1, 3, 5, 7, 9, 2, 4, 6, 8, 10], [10, 9, 8, 7, 6, 5, 4, 3, 2, 1] },
                { [2, 5, 8, 1, 3, 9, 7, 4, 10, 6], [10, 9, 8, 7, 6, 5, 4, 3, 2, 1] },
                { [6, 10, 4, 7, 9, 3, 1, 8, 5, 2], [10, 9, 8, 7, 6, 5, 4, 3, 2, 1] },
            };
        }
    }

    [Theory]
    [MemberData(nameof(OrderAsArrayData))]
    public void OrderDescendingAsArray(ImmutableArray<int> data, ImmutableArray<int> expected)
    {
        var sorted = data.OrderAsArray();
        Assert.Equal<int>(expected, sorted);
    }

    public readonly record struct ValueHolder(int Value)
    {
        public static implicit operator ValueHolder(int value)
            => new(value);
    }

    public static TheoryData<ImmutableArray<ValueHolder>, ImmutableArray<ValueHolder>> OrderByAsArrayData
    {
        get
        {
            return new()
            {
                { [1, 2, 3, 4, 5, 6, 7, 8, 9, 10], [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] },
                { [10, 9, 8, 7, 6, 5, 4, 3, 2, 1], [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] },
                { [1, 3, 5, 7, 9, 2, 4, 6, 8, 10], [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] },
                { [2, 5, 8, 1, 3, 9, 7, 4, 10, 6], [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] },
                { [6, 10, 4, 7, 9, 3, 1, 8, 5, 2], [1, 2, 3, 4, 5, 6, 7, 8, 9, 10] },
            };
        }
    }

    [Theory]
    [MemberData(nameof(OrderByAsArrayData))]
    public void OrderByAsArray(ImmutableArray<ValueHolder> data, ImmutableArray<ValueHolder> expected)
    {
        var sorted = data.OrderByAsArray(static x => x.Value);
        Assert.Equal<ValueHolder>(expected, sorted);
    }

    public static TheoryData<ImmutableArray<ValueHolder>, ImmutableArray<ValueHolder>> OrderByDescendingAsArrayData
    {
        get
        {
            return new()
            {
                { [1, 2, 3, 4, 5, 6, 7, 8, 9, 10], [10, 9, 8, 7, 6, 5, 4, 3, 2, 1] },
                { [10, 9, 8, 7, 6, 5, 4, 3, 2, 1], [10, 9, 8, 7, 6, 5, 4, 3, 2, 1] },
                { [1, 3, 5, 7, 9, 2, 4, 6, 8, 10], [10, 9, 8, 7, 6, 5, 4, 3, 2, 1] },
                { [2, 5, 8, 1, 3, 9, 7, 4, 10, 6], [10, 9, 8, 7, 6, 5, 4, 3, 2, 1] },
                { [6, 10, 4, 7, 9, 3, 1, 8, 5, 2], [10, 9, 8, 7, 6, 5, 4, 3, 2, 1] },
            };
        }
    }

    [Theory]
    [MemberData(nameof(OrderByDescendingAsArrayData))]
    public void OrderByDescendingAsArray(ImmutableArray<ValueHolder> data, ImmutableArray<ValueHolder> expected)
    {
        var sorted = data.OrderByDescendingAsArray(static x => x.Value);
        Assert.Equal<ValueHolder>(expected, sorted);
    }
}
