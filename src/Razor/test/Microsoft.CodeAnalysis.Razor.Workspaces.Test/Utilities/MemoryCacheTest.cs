// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Razor.Utilities;

public class MemoryCacheTest(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    [Fact]
    public async Task ConcurrentSets_DoesNotThrow()
    {
        // Arrange
        var cache = new MemoryCache<string, IReadOnlyList<int>>();
        var entries = Enumerable.Range(0, 500);
        var repeatCount = 4;

        // 1111 2222 3333 4444 ...
        var repeatedEntries = entries.SelectMany(entry => Enumerable.Repeat(entry, repeatCount));
        var tasks = repeatedEntries.Select(async entry =>
        {
            // 2 is an arbitrarily low number, we're just trying to emulate concurrency
            await Task.Delay(2);
            cache.Set(entry.ToString(), value: []);
        });

        // Act & Assert
        await Task.WhenAll(tasks);
    }

    [Fact]
    public void LastAccessIsUpdated()
    {
        var cache = new MemoryCache<string, IReadOnlyList<int>>();
        var cacheAccessor = cache.GetTestAccessor();

        var key = GetNewKey();

        cache.Set(key, value: []);
        Assert.True(cacheAccessor.TryGetLastAccess(key, out var oldAccessTime));

        Thread.Sleep(millisecondsTimeout: 10);

        cache.TryGetValue(key, out _);
        Assert.True(cacheAccessor.TryGetLastAccess(key, out var newAccessTime));

        Assert.True(newAccessTime > oldAccessTime, "New AccessTime should be greater than old");
    }

    [Fact]
    public void BasicAdd()
    {
        var cache = new MemoryCache<string, IReadOnlyList<int>>();
        var key = GetNewKey();
        var value = new List<int> { 1, 2, 3 };

        cache.Set(key, value);

        cache.TryGetValue(key, out var result);

        Assert.Same(value, result);
    }

    [Fact]
    public void Compaction()
    {
        const int SizeLimit = 10;

        var cache = new MemoryCache<string, IReadOnlyList<int>>(SizeLimit);
        var cacheAccessor = cache.GetTestAccessor();

        var wasCompacted = false;
        cacheAccessor.Compacted += () => wasCompacted = true;

        for (var i = 0; i < SizeLimit; i++)
        {
            cache.Set(GetNewKey(), [i]);
            Assert.False(wasCompacted, "It got compacted early.");
        }

        cache.Set(GetNewKey(), [SizeLimit]);
        Assert.True(wasCompacted, "Compaction is not happening");
    }

    [Fact]
    public void MissingKey()
    {
        var cache = new MemoryCache<string, IReadOnlyList<int>>();
        var key = GetNewKey();

        Assert.False(cache.TryGetValue(key, out _));
    }

    [Fact]
    public void NullKey()
    {
        var cache = new MemoryCache<string, IReadOnlyList<int>>();

        Assert.Throws<ArgumentNullException>(() => cache.TryGetValue(key: null!, out var result));
    }

    private static string GetNewKey()
        => Guid.NewGuid().ToString();
}
