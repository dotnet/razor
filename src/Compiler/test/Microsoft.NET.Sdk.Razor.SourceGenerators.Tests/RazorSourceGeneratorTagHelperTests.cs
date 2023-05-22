// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Xunit;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

public sealed class RazorSourceGeneratorTagHelperTests : RazorSourceGeneratorTestsBase
{
    [Fact]
    public async Task CustomTagHelper()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                @addTagHelper *, TestProject

                <email>
                    custom tag helper
                    <email>nested tag helper</email>
                </email>
                """
        }, new()
        {
            ["EmailTagHelper.cs"] = """
                using Microsoft.AspNetCore.Razor.TagHelpers;

                public class EmailTagHelper : TagHelper
                {
                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        output.TagName = "a";
                    }
                }
                """
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        Assert.Contains("EmailTagHelper", result.GeneratedSources.Single().SourceText.ToString());
        result.VerifyOutputsMatchBaseline();
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact]
    public async Task ViewComponent()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                @addTagHelper *, TestProject
                @{
                    var num = 42;
                }

                <vc:test text="Razor" number="@num" flag />
                """,
        }, new()
        {
            ["TestViewComponent.cs"] = """
                public class TestViewComponent
                {
                    public string Invoke(string text, int number, bool flag)
                    {
                        return text;
                    }
                }
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        Assert.Contains("HtmlTargetElementAttribute(\"vc:test\")", result.GeneratedSources.Single().SourceText.ToString());
        result.VerifyOutputsMatchBaseline();
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8718")]
    public async Task ComponentAndTagHelper()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                @addTagHelper *, TestProject

                <email mail="example">custom tag helper</email>
                """,
            ["Shared/EmailTagHelper.razor"] = """
                @inherits ComponentAndTagHelper
                @code {
                    public string? Mail { get; set; }
                }
                """,
        }, new()
        {
            ["EmailTagHelper.cs"] = """
                using Microsoft.AspNetCore.Razor.TagHelpers;
                namespace MyApp.Shared;

                public abstract class ComponentAndTagHelper : TagHelper
                {
                    protected abstract void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder);
                }

                public partial class EmailTagHelper : ComponentAndTagHelper
                {
                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        output.TagName = "a";
                        output.Attributes.SetAttribute("href", $"mailto:{Mail}");
                    }
                }
                """
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.GeneratedSources.Length);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8718")]
    public async Task ComponentAndTagHelper_HtmlTargetElement()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                @addTagHelper *, TestProject

                <email mail="example1">inside email</email>
                <mail mail="example2">inside mail</mail>
                """,
            ["Shared/EmailTagHelper.razor"] = """
                @using Microsoft.AspNetCore.Razor.TagHelpers;
                @attribute [HtmlTargetElement("mail")]
                @inherits ComponentAndTagHelper
                @code {
                    public string? Mail { get; set; }
                }
                """,
        }, new()
        {
            ["EmailTagHelper.cs"] = """
                using Microsoft.AspNetCore.Razor.TagHelpers;
                namespace MyApp.Shared;

                public abstract class ComponentAndTagHelper : TagHelper
                {
                    protected abstract void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder);
                }

                public partial class EmailTagHelper : ComponentAndTagHelper
                {
                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        output.TagName = "a";
                        output.Attributes.SetAttribute("href", $"mailto:{Mail}");
                    }
                }
                """
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.GeneratedSources.Length);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }
}
