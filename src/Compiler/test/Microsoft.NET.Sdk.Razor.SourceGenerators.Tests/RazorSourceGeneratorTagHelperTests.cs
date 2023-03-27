// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Linq;
using System.Threading.Tasks;
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
    public async Task UnboundDynamicAttributes()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            // https://github.com/dotnet/aspnetcore/blob/b40cc0b/src/Mvc/test/WebSites/TagHelpersWebSite/Views/Home/UnboundDynamicAttributes.cshtml
            ["Views/Home/Index.cshtml"] = """
                @addTagHelper AddProcessedAttributeTagHelper, TestProject

                @{
                    var trueVar = true;
                    var falseVar = false;
                    var stringVar = "value";
                    string? nullVar = null;
                }

                @functions {
                    public Task DoSomething()
                    {
                        return Task.FromResult(true);
                    }
                }

                <input checked="@true" />
                <input checked="@trueVar" />
                <input checked="@false" />
                <input checked="@falseVar" />
                <input checked="  @true    " />
                <input checked="  @falseVar    " />
                <input checked="    @stringVar: @trueVar   " />
                <input checked="    value: @false   " />
                <input checked="@true @trueVar" />
                <input checked="   @falseVar  @true" />
                <input checked="@null" />
                <input checked="  @nullVar" />
                <input checked="@nullVar   " />
                <input checked="  @null @stringVar @trueVar" />
                <input checked=" @if (trueVar) { <text>True</text> } else { await DoSomething(); <text>False</text> } " />
                """
        }, new()
        {
            ["AddProcessedAttributeTagHelper.cs"] = """
                using Microsoft.AspNetCore.Razor.TagHelpers;

                [HtmlTargetElement("input")]
                public class AddProcessedAttributeTagHelper : TagHelper
                {
                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        output.Attributes.Add(new TagHelperAttribute("processed"));
                    }
                }
                """
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        RunGenerator(compilation!, ref driver, out compilation);

        // Assert
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
}
