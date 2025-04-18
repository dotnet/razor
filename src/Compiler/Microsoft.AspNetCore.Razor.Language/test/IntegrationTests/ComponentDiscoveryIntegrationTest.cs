﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests;

public class ComponentDiscoveryIntegrationTest : RazorIntegrationTestBase
{
    internal override RazorFileKind? FileKind => RazorFileKind.Component;

    internal override bool UseTwoPhaseCompilation => true;

    [Fact]
    public void ComponentDiscovery_CanFindComponent_DefinedinCSharp()
    {
        // Arrange
        AdditionalSyntaxTrees.Add(Parse(@"
using Microsoft.AspNetCore.Components;

namespace Test
{
    public class MyComponent : ComponentBase
    {
    }
}
"));

        // Act
        var result = CompileToCSharp(string.Empty);

        // Assert
        var context = result.CodeDocument.GetTagHelperContext();
        Assert.NotNull(context);
        Assert.Contains(context.TagHelpers, t => t.Name == "Test.MyComponent");
    }

    [Fact]
    public void ComponentDiscovery_CanFindComponent_WithNamespace_DefinedinCSharp()
    {
        // Arrange
        AdditionalSyntaxTrees.Add(Parse(@"
using Microsoft.AspNetCore.Components;

namespace Test.AnotherNamespace
{
    public class MyComponent : ComponentBase
    {
    }
}
"));

        // Act
        var result = CompileToCSharp(string.Empty);

        // Assert
        var context = result.CodeDocument.GetTagHelperContext();
        Assert.NotNull(context);

        Assert.Contains(context.TagHelpers, t =>
        {
            return t.Name == "Test.AnotherNamespace.MyComponent" &&
                t.IsComponentFullyQualifiedNameMatch;
        });

        Assert.DoesNotContain(context.TagHelpers, t =>
        {
            return t.Name == "Test.AnotherNamespace.MyComponent" &&
                !t.IsComponentFullyQualifiedNameMatch;
        });
    }

    [Fact]
    public void ComponentDiscovery_CanFindComponent_DefinedinCshtml()
    {
        // Arrange

        // Act
        var result = CompileToCSharp("UniqueName.cshtml", cshtmlContent: string.Empty);

        // Assert
        var context = result.CodeDocument.GetTagHelperContext();
        Assert.NotNull(context);

        Assert.Contains(context.TagHelpers, t => t.Name == "Test.UniqueName");
    }

    [Fact]
    public void ComponentDiscovery_CanFindComponent_WithTypeParameter()
    {
        // Arrange

        // Act
        var result = CompileToCSharp("UniqueName.cshtml", cshtmlContent: @"
@typeparam TItem
@functions {
    [Parameter] public TItem Item { get; set; }
}");

        // Assert
        var context = result.CodeDocument.GetTagHelperContext();
        Assert.NotNull(context);

        Assert.Contains(context.TagHelpers, t => t.Name == "Test.UniqueName<TItem>");
    }

    [Fact]
    public void ComponentDiscovery_CanFindComponent_WithTypeParameterAndSemicolon()
    {
        // Arrange

        // Act
        var result = CompileToCSharp("UniqueName.cshtml", cshtmlContent: @"
@typeparam TItem;
@functions {
    [Parameter] public TItem Item { get; set; }
}");

        // Assert
        var context = result.CodeDocument.GetTagHelperContext();
        Assert.NotNull(context);

        Assert.Contains(context.TagHelpers, t => t.Name == "Test.UniqueName<TItem>");
    }

    [Fact]
    public void ComponentDiscovery_CanFindComponent_WithMultipleTypeParameters()
    {
        // Arrange

        // Act
        var result = CompileToCSharp("UniqueName.cshtml", cshtmlContent: @"
@typeparam TItem1
@typeparam TItem2
@typeparam TItem3
@functions {
    [Parameter] public TItem1 Item { get; set; }
}");

        // Assert
        var context = result.CodeDocument.GetTagHelperContext();
        Assert.NotNull(context);

        Assert.Contains(context.TagHelpers, t => t.Name == "Test.UniqueName<TItem1, TItem2, TItem3>");
    }

    [Fact]
    public void ComponentDiscovery_CanFindComponent_WithMultipleTypeParametersAndMixedSemicolons()
    {
        // Arrange

        // Act
        var result = CompileToCSharp("UniqueName.cshtml", cshtmlContent: @"
@typeparam TItem1
@typeparam TItem2;
@typeparam TItem3
@functions {
    [Parameter] public TItem1 Item { get; set; }
}");

        // Assert
        var context = result.CodeDocument.GetTagHelperContext();
        Assert.NotNull(context);

        Assert.Contains(context.TagHelpers, t => t.Name == "Test.UniqueName<TItem1, TItem2, TItem3>");
    }
}
