// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Utilities.Shared.Test.PooledObjects;

public class PooledSpanBuilderTests
{
    [Fact]
    public void Create_FromSpan_CreatesWithElements()
    {
        var source = new[] { 1, 2, 3, 4 };
        using var builder = PooledSpanBuilder.Create(source);

        Assert.Equal(source.Length, builder.Count);
        Assert.True(builder.Any());
        Assert.Equal(source, builder.AsSpan().ToArray());
    }

    [Fact]
    public void Add_AddsElements()
    {
        using var builder = PooledSpanBuilder<int>.Empty;
        builder.Add(10);
        builder.Add(20);

        Assert.Equal(2, builder.Count);
        Assert.Equal([10, 20], builder.AsSpan().ToArray());
    }

    [Fact]
    public void AddRange_AddsSpan()
    {
        using var builder = PooledSpanBuilder<int>.Empty;
        builder.AddRange(new[] { 1, 2, 3 });

        Assert.Equal([1, 2, 3], builder.AsSpan().ToArray());
    }

    [Fact]
    public void Insert_InsertsAtIndex()
    {
        using var builder = PooledSpanBuilder<int>.Empty;
        builder.AddRange(new[] { 1, 3 });
        builder.Insert(1, 2);

        Assert.Equal([1, 2, 3], builder.AsSpan().ToArray());
    }

    [Fact]
    public void RemoveAt_RemovesElement()
    {
        using var builder = PooledSpanBuilder<int>.Empty;
        builder.AddRange(new[] { 1, 2, 3 });
        builder.RemoveAt(1);

        Assert.Equal([1, 3], builder.AsSpan().ToArray());
    }

    [Fact]
    public void Clear_ResetsCount()
    {
        using var builder = PooledSpanBuilder<int>.Empty;
        builder.AddRange(new[] { 1, 2, 3 });
        builder.Clear();

        Assert.Equal(0, builder.Count);
        Assert.Empty(builder.AsSpan().ToArray());
    }

    [Fact]
    public void ToImmutableAndClear_ReturnsImmutableAndClears()
    {
        using var builder = PooledSpanBuilder<int>.Empty;
        builder.AddRange(new[] { 1, 2, 3 });
        var immutable = builder.ToImmutableAndClear();

        Assert.Equal(new[] { 1, 2, 3 }, immutable);
        Assert.Equal(0, builder.Count);
    }

    [Fact]
    public void PeekAndPop_WorkAsStack()
    {
        using var builder = PooledSpanBuilder<int>.Empty;
        builder.Push(1);
        builder.Push(2);

        Assert.Equal(2, builder.Peek());
        Assert.Equal(2, builder.Pop());
        Assert.Equal(1, builder.Peek());
    }

    [Fact]
    public void TryPop_Empty_ReturnsFalse()
    {
        using var builder = PooledSpanBuilder<int>.Empty;
        var result = builder.TryPop(out var value);

        Assert.False(result);
        Assert.Equal(0, value);
    }

    [Fact]
    public void Any_All_First_Last_Single()
    {
        using var builder = PooledSpanBuilder<int>.Empty;
        builder.AddRange(new[] { 1, 2, 3 });

        Assert.True(builder.Any());
        Assert.True(builder.Any(x => x == 2));
        Assert.True(builder.All(x => x > 0));
        Assert.Equal(1, builder.First());
        Assert.Equal(3, builder.Last());
        Assert.Equal(2, builder.Single(x => x == 2));
    }

    [Fact]
    public void ToArrayAndClear_ReturnsArrayAndClears()
    {
        using var builder = PooledSpanBuilder<int>.Empty;
        builder.AddRange(new[] { 5, 6, 7 });
        var arr = builder.ToArrayAndClear();

        Assert.Equal([5, 6, 7], arr);
        Assert.Equal(0, builder.Count);
    }

    [Fact]
    public void Dispose_ReturnsArrayToPool()
    {
        var builder = new PooledSpanBuilder<int>(4);
        builder.AddRange(new[] { 1, 2, 3, 4 });
        builder.Dispose();

        Assert.Equal(0, builder.Count);
        Assert.Equal(0, builder.Capacity);
    }
}
