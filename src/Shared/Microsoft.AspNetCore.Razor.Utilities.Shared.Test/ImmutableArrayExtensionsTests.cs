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
            ", ",
            ", ",
            "World",
            "WORLD",
            "WoRlD"
        ];

        var mostRecent = items.GetMostRecentUniqueItems(StringComparer.OrdinalIgnoreCase);

        Assert.Collection(mostRecent,
            s => Assert.Equal("HeLlO", s),
            s => Assert.Equal(", ", s),
            s => Assert.Equal("WoRlD", s));
    }
}
