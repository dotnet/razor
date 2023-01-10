﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests;

public class ComponentTemplateIntegrationTest : RazorIntegrationTestBase
{
    internal override string FileKind => FileKinds.Component;

    internal override bool UseTwoPhaseCompilation => true;

    // Razor doesn't parse this as a template, we don't need much special handling for
    // it because it will just be invalid in general.
    [Fact]
    public void Template_ImplicitExpressionInMarkupAttribute_CreatesDiagnostic()
    {
        // Arrange

        // Act
        var generated = CompileToCSharp(@"<div attr=""@<div></div>"" />", throwOnFailure: false);

        // Assert
        var diagnostic = Assert.Single(generated.Diagnostics);
        Assert.Equal("RZ1005", diagnostic.Id);
    }

    [Fact]
    public void Template_ExplicitExpressionInMarkupAttribute_CreatesDiagnostic()
    {
        // Arrange

        // Act
        var generated = CompileToCSharp(@"<div attr=""@(@<div></div>)"" />");

        // Assert
        var diagnostic = Assert.Single(generated.Diagnostics);
        Assert.Equal("RZ9994", diagnostic.Id);
    }

    // Razor doesn't parse this as a template, we don't need much special handling for
    // it because it will just be invalid in general.
    [Fact]
    public void Template_ImplicitExpressionInComponentAttribute_CreatesDiagnostic()
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
        var generated = CompileToCSharp(@"<MyComponent attr=""@<div></div>"" />", throwOnFailure: false);

        // Assert
        Assert.Collection(
            generated.Diagnostics,
            d => Assert.Equal("RZ9986", d.Id),
            d => Assert.Equal("RZ1005", d.Id));
    }

    [Fact]
    public void Template_ExplicitExpressionInComponentAttribute_CreatesDiagnostic()
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
        var generated = CompileToCSharp(@"<MyComponent attr=""@(@<div></div>)"" />");

        // Assert
        var diagnostic = Assert.Single(generated.Diagnostics);
        Assert.Equal("RZ9994", diagnostic.Id);
    }

    [Fact]
    public void Template_ExplicitExpressionInRef_CreatesDiagnostic()
    {
        // Arrange

        // Act
        var generated = CompileToCSharp(@"<div ref=""@(@<div></div>)"" />");

        // Assert
        var diagnostic = Assert.Single(generated.Diagnostics);
        Assert.Equal("RZ9994", diagnostic.Id);
    }


    [Fact]
    public void Template_ExplicitExpressionInBind_CreatesDiagnostic()
    {
        // Arrange

        // Act
        var generated = CompileToCSharp(@"<input type=""text"" bind=""@(@<div></div>)"" />");

        // Assert
        var diagnostic = Assert.Single(generated.Diagnostics);
        Assert.Equal("RZ9994", diagnostic.Id);
    }

    [Fact]
    public void Template_ExplicitExpressionInEventHandler_CreatesDiagnostic()
    {
        // Arrange

        // Act
        var generated = CompileToCSharp(@"<input type=""text"" onchange=""@(@<div></div>)"" />");

        // Assert
        var diagnostic = Assert.Single(generated.Diagnostics);
        Assert.Equal("RZ9994", diagnostic.Id);
    }
}
