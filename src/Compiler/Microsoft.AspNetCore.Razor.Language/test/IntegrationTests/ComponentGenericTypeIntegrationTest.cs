// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests;

public class ComponentGenericTypeIntegrationTest : RazorIntegrationTestBase
{
    private readonly CSharpSyntaxTree GenericContextComponent = Parse(@"
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
namespace Test
{
    public class GenericContext<TItem> : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            var items = (IReadOnlyList<TItem>)Items ?? Array.Empty<TItem>();
            for (var i = 0; i < items.Count; i++)
            {
                if (ChildContent == null)
                {
                    builder.AddContent(i, Items[i]);
                }
                else
                {
                    builder.AddContent(i, ChildContent, new Context() { Index = i, Item = items[i], });
                }
            }
        }

        [Parameter]
        public List<TItem> Items { get; set; }

        [Parameter]
        public RenderFragment<Context> ChildContent { get; set; }

        public class Context
        {
            public int Index { get; set; }
            public TItem Item { get; set; }
        }
    }
}
");

    private readonly CSharpSyntaxTree MultipleGenericParameterComponent = Parse(@"
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
namespace Test
{
    public class MultipleGenericParameter<TItem1, TItem2, TItem3> : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, Item1);
            builder.AddContent(1, Item2);
            builder.AddContent(2, Item3);
        }

        [Parameter]
        public TItem1 Item1 { get; set; }

        [Parameter]
        public TItem2 Item2 { get; set; }

        [Parameter]
        public TItem3 Item3 { get; set; }
    }
}
");

    internal override RazorFileKind? FileKind => RazorFileKind.Component;

    internal override bool UseTwoPhaseCompilation => true;

    [Fact]
    public void GenericComponent_WithoutAnyTypeParameters_TriggersDiagnostic()
    {
        // Arrange
        AdditionalSyntaxTrees.Add(GenericContextComponent);

        // Act
        var generated = CompileToCSharp(@"
<GenericContext />");

        // Assert
        var diagnostic = Assert.Single(generated.RazorDiagnostics);
        Assert.Same(ComponentDiagnosticFactory.GenericComponentTypeInferenceUnderspecified.Id, diagnostic.Id);
        Assert.Equal("""
            The type of component 'GenericContext' cannot be inferred based on the values provided. Consider specifying the type arguments directly using the following attributes: 'TItem'.
            """,
            diagnostic.GetMessage(CultureInfo.CurrentCulture));
    }

    [Fact]
    public void GenericComponent_WithMissingTypeParameters_TriggersDiagnostic()
    {
        // Arrange
        AdditionalSyntaxTrees.Add(MultipleGenericParameterComponent);

        // Act
        var generated = CompileToCSharp(@"
<MultipleGenericParameter TItem1=int />");

        // Assert
        var diagnostic = Assert.Single(generated.RazorDiagnostics);
        Assert.Same(ComponentDiagnosticFactory.GenericComponentMissingTypeArgument.Id, diagnostic.Id);
        Assert.Equal("""
            The component 'MultipleGenericParameter' is missing required type arguments. Specify the missing types using the attributes: 'TItem2', 'TItem3'.
            """,
            diagnostic.GetMessage(CultureInfo.CurrentCulture));
    }

    [Fact]
    public void GenericAndNonGenericComponents_WithSameName_CanCoexist()
    {
        // Arrange
        // Create a non-generic component named SomeComponent
        var nonGenericComponent = Parse(@"
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
namespace Test
{
    public class SomeComponent : ComponentBase
    {
        [Parameter]
        public RenderFragment ChildContent { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, ""div"");
            builder.AddContent(1, ChildContent);
            builder.CloseElement();
        }
    }
}
");

        // Create a generic component named SomeComponent<TItem>
        var genericComponent = Parse(@"
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
namespace Test
{
    public class SomeComponent<TItem> : ComponentBase
    {
        [Parameter]
        public RenderFragment<TItem> ChildContent { get; set; }
        
        [Parameter]
        public IReadOnlyCollection<TItem> Items { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, ""div"");
            foreach (var item in Items)
            {
                builder.AddContent(1, ChildContent(item));
            }
            builder.CloseElement();
        }
    }
}
");

        AdditionalSyntaxTrees.Add(nonGenericComponent);
        AdditionalSyntaxTrees.Add(genericComponent);

        // Act - Use the non-generic component (no type parameters provided)
        var nonGenericGenerated = CompileToCSharp(@"
<SomeComponent>
    <p>Non-generic content</p>
</SomeComponent>");

        // Assert - No diagnostics should be generated for non-generic usage
        Assert.Empty(nonGenericGenerated.RazorDiagnostics);

        // Act - Use the generic component (type parameter provided)
        var genericGenerated = CompileToCSharp(@"
<SomeComponent TItem=""string"" Items=""@(new[] { ""a"", ""b"", ""c"" })"">
    <p>Item: @context</p>
</SomeComponent>");

        // Assert - No diagnostics should be generated for generic usage
        Assert.Empty(genericGenerated.RazorDiagnostics);
    }

    [Fact]
    public void GenericAndNonGenericComponents_WithSameName_NonGenericUsage()
    {
        // Arrange
        var nonGenericComponent = Parse(@"
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
namespace Test
{
    public class MyComponent : ComponentBase
    {
        [Parameter]
        public string Message { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, ""div"");
            builder.AddContent(1, Message);
            builder.CloseElement();
        }
    }
}
");

        var genericComponent = Parse(@"
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
namespace Test
{
    public class MyComponent<TItem> : ComponentBase
    {
        [Parameter]
        public TItem Item { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, ""div"");
            builder.AddContent(1, Item);
            builder.CloseElement();
        }
    }
}
");

        AdditionalSyntaxTrees.Add(nonGenericComponent);
        AdditionalSyntaxTrees.Add(genericComponent);

        // Act
        var generated = CompileToCSharp(@"
<MyComponent Message=""Hello"" />");

        // Assert - Should use non-generic component without errors
        Assert.Empty(generated.RazorDiagnostics);
        Assert.Contains("global::Test.MyComponent", generated.Code);
        Assert.DoesNotContain("global::Test.MyComponent<", generated.Code);
    }

    [Fact]
    public void GenericAndNonGenericComponents_WithSameName_GenericUsage()
    {
        // Arrange
        var nonGenericComponent = Parse(@"
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
namespace Test
{
    public class MyComponent : ComponentBase
    {
        [Parameter]
        public string Message { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, ""div"");
            builder.AddContent(1, Message);
            builder.CloseElement();
        }
    }
}
");

        var genericComponent = Parse(@"
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
namespace Test
{
    public class MyComponent<TItem> : ComponentBase
    {
        [Parameter]
        public TItem Item { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, ""div"");
            builder.AddContent(1, Item);
            builder.CloseElement();
        }
    }
}
");

        AdditionalSyntaxTrees.Add(nonGenericComponent);
        AdditionalSyntaxTrees.Add(genericComponent);

        // Act
        var generated = CompileToCSharp(@"
<MyComponent TItem=""int"" Item=""42"" />");

        // Assert - Should use generic component without errors
        Assert.Empty(generated.RazorDiagnostics);
        Assert.Contains("global::Test.MyComponent<", generated.Code);
    }
}
