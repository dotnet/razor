// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Xunit;
using SR = Microsoft.AspNetCore.Razor.Utilities.Shared.Resources.SR;

namespace Microsoft.AspNetCore.Razor.Utilities.Shared.Test.PooledObjects;

public class PooledArrayBuilderTests
{
    [Theory]
    [CombinatorialData]
    public void AddElements([CombinatorialRange(0, 8)] int count)
    {
        using var builder = new PooledArrayBuilder<int>();

        for (var i = 0; i < count; i++)
        {
            builder.Add(i);
        }

        for (var i = 0; i < count; i++)
        {
            Assert.Equal(i, builder[i]);
        }

        var result = builder.DrainToImmutable();

        for (var i = 0; i < count; i++)
        {
            Assert.Equal(i, result[i]);
        }
    }

    public static TheoryData<int, int> RemoveAtIndex_Data
    {
        get
        {
            var data = new TheoryData<int, int>();

            for (var count = 0; count < 8; count++)
            {
                for (var removeIndex = 0; removeIndex < 8; removeIndex++)
                {
                    if (removeIndex < count)
                    {
                        data.Add(count, removeIndex);
                    }
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(RemoveAtIndex_Data))]
    public void RemoveAtIndex(int count, int removeIndex)
    {
        using var builder = new PooledArrayBuilder<int>();

        for (var i = 0; i < count; i++)
        {
            builder.Add(i);
        }

        var newCount = count;
        var newValue = removeIndex;

        // Now, remove each element at removeIndex.
        for (var i = removeIndex; i < builder.Count; i++)
        {
            builder.RemoveAt(removeIndex);
            newCount--;
            newValue++;

            Assert.Equal(newCount, builder.Count);

            // Check the values starting at removeIndex.
            for (var j = removeIndex; j < newCount; j++)
            {
                Assert.Equal(newValue + (j - removeIndex), builder[j]);
            }
        }
    }

    [Fact]
    public void FirstAndLast()
    {
        using var builder = new PooledArrayBuilder<int>();
        builder.Add(19);
        builder.Add(23);

        Assert.Equal(19, builder.First());
        Assert.Equal(19, builder.FirstOrDefault());
        Assert.Equal(23, builder.Last());
        Assert.Equal(23, builder.LastOrDefault());

        builder.Clear();
        Assert.Equal(default, builder.FirstOrDefault());
        Assert.Equal(default, builder.LastOrDefault());
    }

    [Fact]
    public void Single()
    {
        using var builder = new PooledArrayBuilder<int>();

        var exception1 = Assert.Throws<InvalidOperationException>(() => builder.Single());
        Assert.Equal(SR.Contains_no_elements, exception1.Message);
        Assert.Equal(default, builder.SingleOrDefault());

        builder.Add(19);

        Assert.Equal(19, builder.Single());
        Assert.Equal(19, builder.SingleOrDefault());

        builder.Add(23);

        var exception2 = Assert.Throws<InvalidOperationException>(() => builder.Single());
        Assert.Equal(SR.Contains_more_than_one_element, exception2.Message);
        Assert.Equal(default, builder.SingleOrDefault());
    }
}
