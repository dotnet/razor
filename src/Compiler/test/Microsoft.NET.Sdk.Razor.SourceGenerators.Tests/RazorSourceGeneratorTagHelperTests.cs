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

    [Fact]
    public async Task ViewComponentTagHelpers()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            // https://github.com/dotnet/aspnetcore/blob/b40cc0b/src/Mvc/test/WebSites/TagHelpersWebSite/Views/Home/ViewComponentTagHelpers.cshtml
            ["Views/Home/Index.cshtml"] = """
                @addTagHelper "*, TestProject"
                @{ 
                    var year = 2016;
                    var dict = new Dictionary<string, List<string>>();
                    var items = new List<string>() { "One", "Two", "Three" };
                    dict.Add("Foo", items);
                }

                <vc:generic items="dict"></vc:generic>
                <!-- <vc:generic items-foo="items"></vc:generic> -->
                <vc:duck beak-color="Green" /><br />
                <div>
                    <vc:copyright website="example.com" year="@year"></vc:copyright>
                </div>
                """,
            ["Views/Shared/Components/Generic/Default.cshtml"] = """
                @model Dictionary<string, List<string>>
                <div>Items: </div>
                <div>
                    @foreach (var item in Model)
                    {
                        <strong>@item.Key</strong><br/>
                        @foreach (var value in Model[item.Key])
                        {
                            <span>@value</span>
                        }
                    }
                </div>
                """,
            ["Views/Shared/Components/Duck/Default.cshtml"] = """
                @model string
                <div id="ascii" style="font-family:Courier New, Courier, monospace; font-size: 6px">
                    @Html.Raw(Model)
                </div>
                """,
            ["Views/Shared/Components/Copyright/Default.cshtml"] = """
                @model Dictionary<string, object>
                <footer>Copyright @Model["year"] @Model["website"]</footer>
                """,
        }, new()
        {
            ["GlobalUsings.g.cs"] = """
                global using System;
                global using System.Collections.Generic;
                """,
            ["GenericViewComponent.cs"] = """
                using Microsoft.AspNetCore.Mvc;

                public class GenericViewComponent : ViewComponent
                {
                    public IViewComponentResult Invoke(Dictionary<string, List<string>> items)
                    {
                        return View(items);
                    }
                }
                """,
            ["CopyrightViewComponent.cs"] = """
                using Microsoft.AspNetCore.Mvc;

                public class CopyrightViewComponent : ViewComponent
                {
                    public IViewComponentResult Invoke(string website, int year)
                    {
                        var dict = new Dictionary<string, object>
                        {
                            ["website"] = website,
                            ["year"] = year
                        };

                        return View(dict);
                    }
                }
                """,
            ["BeakColor.cs"] = """
                public enum BeakColor
                {
                    Red,
                    Blue,
                    Green,
                    Navy,
                    Brown,
                    Purple
                }
                """,
            ["DuckViewComponent.cs"] = """
                using System.Globalization;
                using Microsoft.AspNetCore.Mvc;

                public class DuckViewComponent : ViewComponent
                {
                    public IViewComponentResult Invoke(BeakColor beakColor)
                    {
                        var colorReplacement = string.Format(CultureInfo.InvariantCulture, "<span style='color:{0}'>&lt;</span>", beakColor);

                        var resultString = DuckString
                            .Replace("<", colorReplacement)
                            .Replace(Environment.NewLine, "<br>")
                            .Replace("\n", "<br>");

                        return View<string>(resultString);
                    }

                    private const string DuckString = @"
                          __
                        <(o )___
                         ( ._> /
                          `---'
                    ";
                }
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact]
    public async Task WebsiteInformationTagHelper()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            // https://github.com/dotnet/aspnetcore/blob/b40cc0b/src/Mvc/test/WebSites/TagHelpersWebSite/Views/Home/About.cshtml
            ["Views/Home/Index.cshtml"] = """
                @using TestProject.Models

                @{
                    ViewBag.Title = "About";
                }

                @addTagHelper ATagHelper, TestProject
                @addTagHelper WebsiteInformationTagHelper, TestProject

                <div>
                    <p>Hello, you've reached the about page.</p>

                    <h3>Information about our website (outdated):</h3>
                    <website-information info="new WebsiteContext {
                                                    Version = new Version(1, 1),
                                                    CopyrightYear = 1990,
                                                    Approved = true,
                                                    TagsToShow = 30 }"/>
                </div>
                """
        }, new()
        {
            ["Models/WebsiteContext.cs"] = """
                using System;

                namespace TestProject.Models;

                public class WebsiteContext
                {
                    public required Version Version { get; set; }

                    public int CopyrightYear { get; set; }

                    public bool Approved { get; set; }

                    public int TagsToShow { get; set; }
                }
                """,
            ["WebsiteInformationTagHelper.cs"] = """
                using System;
                using System.Globalization;
                using Microsoft.AspNetCore.Razor.TagHelpers;
                using TestProject.Models;

                public class WebsiteInformationTagHelper : TagHelper
                {
                    public required WebsiteContext Info { get; set; }

                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        output.TagName = "section";
                        output.PostContent.AppendHtml(string.Format(
                            CultureInfo.InvariantCulture,
                            "<p><strong>Version:</strong> {0}</p>" + Environment.NewLine +
                            "<p><strong>Copyright Year:</strong> {1}</p>" + Environment.NewLine +
                            "<p><strong>Approved:</strong> {2}</p>" + Environment.NewLine +
                            "<p><strong>Number of tags to show:</strong> {3}</p>" + Environment.NewLine,
                            Info.Version,
                            Info.CopyrightYear,
                            Info.Approved,
                            Info.TagsToShow));
                        output.TagMode = TagMode.StartTagAndEndTag;
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
}
