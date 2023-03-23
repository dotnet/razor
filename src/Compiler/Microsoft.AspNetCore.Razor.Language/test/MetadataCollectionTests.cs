// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language;

public class MetadataCollectionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    public void CreateAndCompareCollections(int size)
    {
        var pairs = new List<KeyValuePair<string, string>>();

        for (var i = 0; i < size; i++)
        {
            var key = i.ToString(CultureInfo.InvariantCulture);
            var value = (int.MaxValue - i).ToString(CultureInfo.InvariantCulture);

            pairs.Add(new KeyValuePair<string, string>(key, value));
        }

        var collection1 = MetadataCollection.Create(pairs.ToArray());
        var collection2 = MetadataCollection.Create(pairs.ToArray().Reverse().ToArray());

        Assert.Equal(collection1, collection2);

        foreach (var (key, value) in pairs)
        {
            Assert.True(collection1.TryGetValue(key, out var value1));
            Assert.Equal(value, value1);

            Assert.True(collection2.TryGetValue(key, out var value2));
            Assert.Equal(value, value2);
        }
    }
}
