// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
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
    public async Task TagHelpersWebSite()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            // https://github.com/dotnet/aspnetcore/blob/b40cc0b/src/Mvc/test/WebSites/TagHelpersWebSite/Views/Home/Index.cshtml
            ["Views/Home/Index.cshtml"] = """
                @using TestProject.Models
                @using Microsoft.AspNetCore.Mvc.Razor

                @model WebsiteContext
                @{
                    ViewBag.Title = "Home Page";
                    ViewData.Model = new()
                    {
                        Approved = false,
                        CopyrightYear = 2015,
                        Version = new Version(1, 3, 3, 7),
                        TagsToShow = 20
                    };
                }

                @addTagHelper *, TestProject

                @section css {
                    <style condition="!Model.Approved">
                        h1 {
                            color:red;
                            font-size:2em;
                        }
                    </style>
                }

                @functions {
                    public void RenderTemplate(string title, Func<string, HelperResult> template)
                    {
                        Output.WriteLine("<br /><p><em>Rendering Template:</em></p>");
                        var helperResult = template(title);
                        helperResult.WriteTo(Output, HtmlEncoder);
                    }
                }

                <div condition="!Model.Approved">
                    <p>This website has <strong surround="em">not</strong> been approved yet. Visit www.contoso.com for <strong make-pretty="false">more</strong> information.</p>
                </div>

                <div>
                    <h3>Current Tag Cloud from Tag Helper</h3>
                    <tag-cloud count="Model.TagsToShow" surround="div" />
                    <h3>Current Tag Cloud from ViewComponentHelper:</h3>
                    <section bold>@await Component.InvokeAsync("Tags", new { count = 15 })</section>
                    @{
                        RenderTemplate(
                            "Tag Cloud from Template: ",
                            @<div condition="true"><h3>@item</h3><tag-cloud count="Model.TagsToShow"></tag-cloud></div>);
                    }
                </div>

                <div>
                    <h3>Dictionary Valued Model Expression</h3>
                    <div prefix-test1="@Model.TagsToShow" prefix-test2="@Model.Version.Build"></div>
                </div>

                @section footerContent {
                    <p condition="Model.Approved" bold surround="section">&copy; @Model.CopyrightYear - My ASP.NET Application</p>
                }
                """,
            ["Views/Shared/Layout.cshtml"] = """
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <title>@ViewBag.Title - My ASP.NET Application</title>

                    @RenderSection("css", required: false)
                </head>
                <body>
                    <h1>ASP.NET vNext - @ViewBag.Title</h1>
                    <div>
                        @RenderBody()
                        <hr />
                        <footer>
                            @RenderSection("footerContent", required: false)
                        </footer>
                    </div>
                </body>
                </html>
                """
        }, new()
        {
            ["GlobalUsings.g.cs"] = """
                global using System;
                global using System.Collections.Generic;
                global using System.IO;
                global using System.Linq;
                global using System.Threading.Tasks;
                """,
            ["Models/WebsiteContext.cs"] = """
                namespace TestProject.Models;

                public class WebsiteContext
                {
                    public Version Version { get; set; }

                    public int CopyrightYear { get; set; }

                    public bool Approved { get; set; }

                    public int TagsToShow { get; set; }
                }
                """,
            ["AutoLinkerTagHelper.cs"] = """
                using System.Text.RegularExpressions;
                using Microsoft.AspNetCore.Razor.TagHelpers;

                namespace TestProject.TagHelpers;

                [HtmlTargetElement("p")]
                public class AutoLinkerTagHelper : TagHelper
                {
                    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
                    {
                        var childContent = await output.GetChildContentAsync();

                        // Find Urls in the content and replace them with their anchor tag equivalent.
                        output.Content.AppendHtml(Regex.Replace(
                            childContent.GetContent(),
                            @"\b(?:https?://|www\.)(\S+)\b",
                            "<strong><a target=\"_blank\" href=\"http://$0\">$0</a></strong>"));
                    }
                }
                """,
            ["BoldTagHelper.cs"] = """
                using Microsoft.AspNetCore.Razor.TagHelpers;

                namespace TestProject.TagHelpers;

                [HtmlTargetElement(Attributes = "bold")]
                public class BoldTagHelper : TagHelper
                {
                    public override int Order
                    {
                        get
                        {
                            return int.MinValue;
                        }
                    }

                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        output.Attributes.RemoveAll("bold");
                        output.PreContent.AppendHtml("<b>");
                        output.PostContent.AppendHtml("</b>");
                    }
                }
                """,
            ["ConditionTagHelper.cs"] = """
                using Microsoft.AspNetCore.Razor.TagHelpers;

                namespace TestProject.TagHelpers;

                [HtmlTargetElement("div")]
                [HtmlTargetElement("style")]
                [HtmlTargetElement("p")]
                public class ConditionTagHelper : TagHelper
                {
                    public bool? Condition { get; set; }

                    public override int Order
                    {
                        get
                        {
                            // Run after other tag helpers targeting the same element. Other tag helpers have Order <= 0.
                            return 1000;
                        }
                    }

                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        // If a condition is set and evaluates to false, don't render the tag.
                        if (Condition.HasValue && !Condition.Value)
                        {
                            output.SuppressOutput();
                        }
                    }
                }
                """,
            ["DictionaryPrefixTestTagHelper.cs"] = """
                using Microsoft.AspNetCore.Mvc.Rendering;
                using Microsoft.AspNetCore.Mvc.ViewFeatures;
                using Microsoft.AspNetCore.Razor.TagHelpers;

                namespace TestProject.TagHelpers;

                [HtmlTargetElement(Attributes = "prefix-*")]
                public class DictionaryPrefixTestTagHelper : TagHelper
                {
                    [HtmlAttributeName(DictionaryAttributePrefix = "prefix-")]
                    public IDictionary<string, ModelExpression> PrefixValues { get; set; } = new Dictionary<string, ModelExpression>();

                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        var ulTag = new TagBuilder("ul");

                        foreach (var item in PrefixValues)
                        {
                            var liTag = new TagBuilder("li");

                            liTag.InnerHtml.Append(item.Value.Name);

                            ulTag.InnerHtml.AppendHtml(liTag);
                        }

                        output.Content.SetHtmlContent(ulTag);
                    }
                }
                """,
            ["PrettyTagHelper.cs"] = """
                using Microsoft.AspNetCore.Mvc.Rendering;
                using Microsoft.AspNetCore.Mvc.ViewFeatures;
                using Microsoft.AspNetCore.Razor.TagHelpers;

                namespace TestProject.TagHelpers;

                [HtmlTargetElement("*")]
                public class PrettyTagHelper : TagHelper
                {
                    private static readonly Dictionary<string, string> PrettyTagStyles =
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "a", "background-color: gray;color: white;border-radius: 3px;"
                                + "border: 1px solid black;padding: 3px;font-family: cursive;" },
                            { "strong", "font-size: 1.25em;text-decoration: underline;" },
                            { "h1", "font-family: cursive;" },
                            { "h3", "font-family: cursive;" }
                        };

                    public bool? MakePretty { get; set; }

                    public string Style { get; set; }

                    [ViewContext]
                    [HtmlAttributeNotBound]
                    public ViewContext ViewContext { get; set; }

                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        if (MakePretty.HasValue && !MakePretty.Value)
                        {
                            return;
                        }

                        if (output.TagName == null)
                        {
                            // Another tag helper e.g. TagCloudViewComponentTagHelper has suppressed the start and end tags.
                            return;
                        }

                        string prettyStyle;

                        if (PrettyTagStyles.TryGetValue(output.TagName, out prettyStyle))
                        {
                            var style = Style ?? string.Empty;
                            if (!string.IsNullOrEmpty(style))
                            {
                                style += ";";
                            }

                            output.Attributes.SetAttribute("style", style + prettyStyle);
                        }
                    }
                }
                """,
            ["SurroundTagHelper.cs"] = """
                using Microsoft.AspNetCore.Razor.TagHelpers;

                namespace TestProject.TagHelpers;

                [HtmlTargetElement(Attributes = nameof(Surround))]
                public class SurroundTagHelper : TagHelper
                {
                    public override int Order
                    {
                        get
                        {
                            // Run first
                            return int.MinValue;
                        }
                    }

                    public string Surround { get; set; }

                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        var surroundingTagName = Surround.ToLowerInvariant();

                        output.PreElement.AppendHtml($"<{surroundingTagName}>");
                        output.PostElement.AppendHtml($"</{surroundingTagName}>");
                    }
                }
                """,
            ["TagCloudViewComponentTagHelper.cs"] = """
                using System.Reflection;
                using System.Text.Encodings.Web;
                using Microsoft.AspNetCore.Mvc;
                using Microsoft.AspNetCore.Mvc.Rendering;
                using Microsoft.AspNetCore.Mvc.ViewComponents;
                using Microsoft.AspNetCore.Mvc.ViewFeatures;
                using Microsoft.AspNetCore.Razor.TagHelpers;

                namespace MvcSample.Web.Components;

                [HtmlTargetElement("tag-cloud")]
                [ViewComponent(Name = "Tags")]
                public class TagCloudViewComponentTagHelper : ITagHelper
                {
                    private static readonly string[] Tags =
                        ("Lorem ipsum dolor sit amet consectetur adipisicing elit sed do eiusmod tempor incididunt ut labore et dolore magna aliqua" +
                         "Ut enim ad minim veniam quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat Duis aute irure " +
                         "dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur Excepteur sint occaecat cupidatat" +
                         "non proident, sunt in culpa qui officia deserunt mollit anim id est laborum")
                            .Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .ToArray();
                    private readonly HtmlEncoder _htmlEncoder;

                    public TagCloudViewComponentTagHelper(HtmlEncoder htmlEncoder)
                    {
                        _htmlEncoder = htmlEncoder;
                    }

                    public int Count { get; set; }

                    [HtmlAttributeNotBound]
                    [ViewContext]
                    public ViewContext ViewContext { get; set; }

                    public int Order { get; } = 0;

                    public void Init(TagHelperContext context)
                    {
                    }

                    public async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
                    {
                        var result = await InvokeAsync(Count);
                        var writer = new StringWriter();

                        var viewComponentDescriptor = new ViewComponentDescriptor()
                        {
                            TypeInfo = typeof(TagCloudViewComponentTagHelper).GetTypeInfo(),
                            ShortName = "TagCloudViewComponentTagHelper",
                            FullName = "TagCloudViewComponentTagHelper",
                        };

                        await result.ExecuteAsync(new ViewComponentContext(
                            viewComponentDescriptor,
                            new Dictionary<string, object>(),
                            _htmlEncoder,
                            ViewContext,
                            writer));

                        output.TagName = null;
                        output.Content.AppendHtml(writer.ToString());
                    }

                    public async Task<IViewComponentResult> InvokeAsync(int count)
                    {
                        var tags = await GetTagsAsync(count);

                        return new ContentViewComponentResult(string.Join(",", tags));
                    }

                    private Task<string[]> GetTagsAsync(int count)
                    {
                        return Task.FromResult(GetTags(count));
                    }

                    private string[] GetTags(int count)
                    {
                        return Tags.Take(count).ToArray();
                    }
                }
                """,
        });
        project = project.WithCompilationOptions(((CSharpCompilationOptions)project.CompilationOptions!)
            .WithNullableContextOptions(CodeAnalysis.NullableContextOptions.Disable));
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        RunGenerator(compilation!, ref driver, out compilation);

        // Assert
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
                    <vc:copyright website="example.com" year="@year" bold></vc:copyright>
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
            ["BoldTagHelper.cs"] = """
                using Microsoft.AspNetCore.Razor.TagHelpers;

                [HtmlTargetElement(Attributes = "bold")]
                public class BoldTagHelper : TagHelper
                {
                    public override int Order
                    {
                        get
                        {
                            return int.MinValue;
                        }
                    }

                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        output.Attributes.RemoveAll("bold");
                        output.PreContent.AppendHtml("<b>");
                        output.PostContent.AppendHtml("</b>");
                    }
                }
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
        RunGenerator(compilation!, ref driver, out compilation);

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
