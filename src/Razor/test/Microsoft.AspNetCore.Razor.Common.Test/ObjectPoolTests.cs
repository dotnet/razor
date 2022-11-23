// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Text;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.Common.Test;

public class ObjectPoolTests : TestBase
{
    public ObjectPoolTests(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    [Fact]
    public void ObjectPoolBehavior()
    {
        var accessor = StringBuilderPool.DefaultPool.GetTestAccessor();
        accessor.Clear();

        // Verify that the pool is empty.
        Assert.Equal(0, accessor.UsedSlotCount);

        // Acquire a StringBuilder.
        var pooledBuilder = StringBuilderPool.GetPooledObject();
        var builder = pooledBuilder.Object;

        // Verify that the pool is still empty.
        Assert.Equal(0, accessor.UsedSlotCount);

        // Add characters to the StringBuilder.
        builder.Append('x', 42);

        // Now, dispose the PooledObject<>. That should clear the StringBuilder and
        // put it back in the pool.
        pooledBuilder.Dispose();

        // Verify that our builder has been cleared, but it's capacity is equal to
        // the number of characters we added.
        Assert.Equal(0, builder.Length);
        Assert.Equal(42, builder.Capacity);

        // The pool should now have a slot used.
        Assert.Equal(1, accessor.UsedSlotCount);

        // Assert that the slot is occupied by the same builder.
        Assert.Same(builder, accessor[0]);

        // Acquire another StringBuilder.
        var pooledBuilder2 = StringBuilderPool.GetPooledObject();
        var builder2 = pooledBuilder2.Object;

        Assert.Equal(0, accessor.UsedSlotCount);

        // This should return the same builder, since it was in the pool.
        Assert.Same(builder, builder2);

        // Add a bunch of characters to the "new" builder.
        builder2.Append('x', PooledObject.Threshold * 2);
        Assert.Equal(PooledObject.Threshold * 2, builder2.Length);
        Assert.Equal(PooledObject.Threshold * 2, builder2.Capacity);

        // Finally, dispose the new PooledObject<> to put the builder back in the pool again.
        pooledBuilder2.Dispose();

        Assert.Equal(1, accessor.UsedSlotCount);

        // The builder should be cleared. However, it's capacity should be 512, which
        // is the threshold used by StringBuilderPool.
        Assert.Equal(0, builder2.Length);
        Assert.Equal(PooledObject.Threshold, builder2.Capacity);
    }

    [Fact]
    public void NestPooledStringBuilders()
    {
        var accessor = StringBuilderPool.DefaultPool.GetTestAccessor();
        accessor.Clear();

        StringBuilder builder1, builder2;

        using (var pooledBuilder1 = StringBuilderPool.GetPooledObject())
        {
            builder1 = pooledBuilder1.Object;

            using var pooledBuilder2 = StringBuilderPool.GetPooledObject();
            builder2 = pooledBuilder2.Object;
        }

        Assert.Equal(2, accessor.UsedSlotCount);

        // The second builder should be in the first slot because it was returned
        // to the pool first.
        Assert.Same(builder2, accessor[0]);
        Assert.Same(builder1, accessor[1]);
    }

    [Fact]
    public void BuildImmutableArray()
    {
        var accessor = ArrayBuilderPool<int>.DefaultPool.GetTestAccessor();
        accessor.Clear();

        ImmutableArray<int> array;

        using (var pooledBuilder = ArrayBuilderPool<int>.GetPooledObject())
        {
            var builder = pooledBuilder.Object;
            builder.Capacity = 100;

            for (var i = 0; i < 100; i++)
            {
                builder.Add(i);
            }

            array = builder.MoveToImmutable();
        }

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(i, array[i]);
        }

        Assert.Equal(1, accessor.UsedSlotCount);
        var slot = accessor[0];
        Assert.NotNull(slot);
        Assert.Empty(slot);
    }
}
