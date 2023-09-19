﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor;

public class DefaultTypeNameFeatureTest
{
    [Theory]
    [InlineData("C", 0)]
    [InlineData("T", 0)]
    [InlineData("T[]", 1)]
    [InlineData("T[][]", 1)]
    [InlineData("(T, T)[]", 2)]
    [InlineData("(T X, T Y)[]", 2)]
    [InlineData("(T[], T)[]", 2)]
    [InlineData("(T[] X, T Y)[]", 2)]
    [InlineData("C<T>", 1)]
    [InlineData("C<T[]>", 1)]
    [InlineData("C<T[][]>", 1)]
    [InlineData("C<(T, T)[]>", 2)]
    [InlineData("C<(T X, T Y)[]>", 2)]
    [InlineData("C<(T[], T)[]>", 2)]
    [InlineData("C<(T[] X, T Y)[]>", 2)]
    public void ParseTypeParameters(string input, int expectedNumberOfTs)
    {
        // Arrange.
        var feature = new DefaultTypeNameFeature();

        // Act.
        var parsed = feature.ParseTypeParameters(input);

        // Assert.
        Assert.Equal(Enumerable.Repeat("T", expectedNumberOfTs), parsed);
    }
}
