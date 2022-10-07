// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor;

public class GlobalQualifiedTypeNameRewriterTest
{
    [Theory]
    [InlineData("String", "global::String")]
    [InlineData("System.String", "global::System.String")]
    [InlineData("TItem2", "TItem2")]
    [InlineData("System.Collections.Generic.List<System.String>", "global::System.Collections.Generic.List<global::System.String>")]
    [InlineData("System.Collections.Generic.Dictionary<System.String, TItem1>", "global::System.Collections.Generic.Dictionary<global::System.String, TItem1>")]
    [InlineData("System.Collections.TItem3.Dictionary<System.String, TItem1>", "global::System.Collections.TItem3.Dictionary<global::System.String, TItem1>")]
    [InlineData("System.Collections.TItem3.TItem1<System.String, TItem1>", "global::System.Collections.TItem3.TItem1<global::System.String, TItem1>")]
    [InlineData("M.RenderFragment<(N.MyClass I1, N.MyStruct I2, TItem1 P)>", "global::M.RenderFragment<(global::N.MyClass I1, global::N.MyStruct I2, TItem1 P)>")]

    // This case is interesting because we know TITem2 to be a generic type parameter,
    // and we know that this will never be valid, which is why we don't bother rewriting.
    [InlineData("TItem2<System.String, TItem1>", "TItem2<global::System.String, TItem1>")]
    public void GlobalQualifiedTypeNameRewriter_CanQualifyNames(string original, string expected)
    {
        // Arrange
        var visitor = new GlobalQualifiedTypeNameRewriter(new[] { "TItem1", "TItem2", "TItem3" });

        // Act
        var actual = visitor.Rewrite(original);

        // Assert
        Assert.Equal(expected, actual.ToString());
    }
}
