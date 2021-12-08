// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    public class HtmlFormattingTest : FormattingTestBase
    {
        internal override bool UseTwoPhaseCompilation => true;

        internal override bool DesignTime => true;

        public HtmlFormattingTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [CombinatorialData]
        public async Task FormatsSimpleHtmlTag(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
   <html>
<head>
   <title>Hello</title></head>
<body><div>
</div>
        </body>
 </html>
",
expected: @"
<html>
<head>
    <title>Hello</title>
</head>
<body>
    <div>
    </div>
</body>
</html>
");
        }

        [Theory]
        [CombinatorialData]
        public async Task FormatsSimpleHtmlTag_Range(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
<html>
<head>
    <title>Hello</title>
</head>
<body>
        [|<div>
        </div>|]
</body>
</html>
",
expected: @"
<html>
<head>
    <title>Hello</title>
</head>
<body>
    <div>
    </div>
</body>
</html>
");
        }

        [Theory]
        [CombinatorialData]
        public async Task FormatsRazorHtmlBlock(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"@page ""/error""

        <h1 class=
""text-danger"">Error.</h1>
    <h2 class=""text-danger"">An error occurred while processing your request.</h2>

            <h3>Development Mode</h3>
<p>
    Swapping to <strong>Development</strong> environment will display more detailed information about the error that occurred.</p>
<p>
    <strong>The Development environment shouldn't be enabled for deployed applications.
</strong>
            <div>
 <div>
    <div>
<div>
        This is heavily nested
</div>
 </div>
    </div>
        </div>
</p>
",
expected: @"@page ""/error""

<h1 class=""text-danger"">
    Error.
</h1>
<h2 class=""text-danger"">An error occurred while processing your request.</h2>

<h3>Development Mode</h3>
<p>
    Swapping to <strong>Development</strong> environment will display more detailed information about the error that occurred.
</p>
<p>
    <strong>
        The Development environment shouldn't be enabled for deployed applications.
    </strong>
    <div>
        <div>
            <div>
                <div>
                    This is heavily nested
                </div>
            </div>
        </div>
    </div>
</p>
");
        }

        [Theory]
        [CombinatorialData]
        public async Task FormatsMixedHtmlBlock(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"@page ""/test""
@{
<p>
        @{
                var t = 1;
if (true)
{

            }
        }
        </p>
<div>
 @{
    <div>
<div>
        This is heavily nested
</div>
 </div>
    }
        </div>
}
",
expected: @"@page ""/test""
@{
    <p>
        @{
            var t = 1;
            if (true)
            {

            }
        }
    </p>
    <div>
        @{
            <div>
                <div>
                    This is heavily nested
                </div>
            </div>
        }
    </div>
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task FormatsMixedRazorBlock(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"@page ""/test""

<div class=@className>Some Text</div>

@{
@: Hi!
var x = 123;
<p>
        @if (true) {
                var t = 1;
if (true)
{
<div>@DateTime.Now</div>
            }

            @while(true){
 }
        }
        </p>
}
",
expected: @"@page ""/test""

<div class=@className>Some Text</div>

@{
    @: Hi!
    var x = 123;
    <p>
        @if (true)
        {
            var t = 1;
            if (true)
            {
                <div>@DateTime.Now</div>
            }

            @while (true)
            {
            }
        }
    </p>
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task FormatsMixedContentWithMultilineExpressions(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"@page ""/test""

<div
attr='val'
class=@className>Some Text</div>

@{
@: Hi!
var x = DateTime
    .Now.ToString();
<p>
        @if (true) {
                var t = 1;
        }
        </p>
}

@(DateTime
    .Now
.ToString())

@(
    Foo.Values.Select(f =>
    {
        return f.ToString();
    })
)
",
expected: @"@page ""/test""

<div attr='val'
     class=@className>
    Some Text
</div>

@{
    @: Hi!
    var x = DateTime
        .Now.ToString();
    <p>
        @if (true)
        {
            var t = 1;
        }
    </p>
}

@(DateTime
    .Now
.ToString())

@(
    Foo.Values.Select(f =>
    {
        return f.ToString();
    })
)
");
        }

        [Theory]
        [CombinatorialData]
        public async Task FormatsComplexBlock(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"@page ""/""

<h1>Hello, world!</h1>

        Welcome to your new app.

<SurveyPrompt Title=""How is Blazor working for you?"" />

<div class=""FF""
     id=""ERT"">
     asdf
    <div class=""3""
         id=""3"">
             @if(true){<p></p>}
         </div>
</div>

@{
<div class=""FF""
    id=""ERT"">
    asdf
    <div class=""3""
        id=""3"">
            @if(true){<p></p>}
        </div>
</div>
}

@functions {
        public class Foo
    {
        @* This is a Razor Comment *@
        void Method() { }
    }
}
",
expected: @"@page ""/""

<h1>Hello, world!</h1>

        Welcome to your new app.

<SurveyPrompt Title=""How is Blazor working for you?"" />

<div class=""FF""
     id=""ERT"">
    asdf
    <div class=""3""
         id=""3"">
        @if (true)
        {
            <p></p>
        }
    </div>
</div>

@{
    <div class=""FF""
         id=""ERT"">
        asdf
        <div class=""3""
             id=""3"">
            @if (true)
            {
                <p></p>
            }
        </div>
    </div>
}

@functions {
    public class Foo
    {
        @* This is a Razor Comment *@
        void Method() { }
    }
}
", tagHelpers: GetSurveyPrompt());
        }

        [Theory]
        [CombinatorialData]
        public async Task FormatsComponentTags(bool useSourceTextDiffer)
        {
            var tagHelpers = GetComponents();
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
   <Counter>
    @if(true){
        <p>@DateTime.Now</p>
}
</Counter>

    <GridTable>
    @foreach (var row in rows){
        <GridRow @onclick=""SelectRow(row)"">
        @foreach (var cell in row){
    <GridCell>@cell</GridCell>}</GridRow>
    }
</GridTable>
",
expected: @"
<Counter>
    @if (true)
    {
        <p>@DateTime.Now</p>
    }
</Counter>

<GridTable>
    @foreach (var row in rows)
    {
        <GridRow @onclick=""SelectRow(row)"">
            @foreach (var cell in row)
            {
                <GridCell>@cell</GridCell>
            }
        </GridRow>
    }
</GridTable>
",
tagHelpers: tagHelpers);
        }

        [Theory]
        [CombinatorialData]
        public async Task FormatsShortBlock(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
                input: @"@{<p></p>}",
                expected: @"@{
    <p></p>
}");
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/26836")]
        public async Task FormatNestedBlock(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"@code {
    public string DoSomething()
    {
        <strong>
            @DateTime.Now.ToString()
        </strong>

        return String.Empty;
    }
}
",
expected: @"@code {
    public string DoSomething()
    {
        <strong>
            @DateTime.Now.ToString()
        </strong>

        return String.Empty;
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/26836")]
        public async Task FormatNestedBlock_Tabs(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"@code {
    public string DoSomething()
    {
        <strong>
            @DateTime.Now.ToString()
        </strong>

        return String.Empty;
    }
}
",
expected: @"@code {
	public string DoSomething()
	{
		<strong>
			@DateTime.Now.ToString()
		</strong>

		return String.Empty;
	}
}
",
tabSize: 4, // Due to a bug in the HTML formatter, this needs to be 4
insertSpaces: false);
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1273468/")]
        public async Task FormatHtmlWithTabs1(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@page ""/""
@{
 ViewData[""Title""] = ""Create"";
 <hr />
 <div class=""row"">
  <div class=""col-md-4"">
   <form method=""post"">
    <div class=""form-group"">
     <label asp-for=""Movie.Title"" class=""control-label""></label>
     <input asp-for=""Movie.Title"" class=""form-control"" />
     <span asp-validation-for=""Movie.Title"" class=""text-danger""></span>
    </div>
   </form>
  </div>
 </div>
}
",
expected: @"@page ""/""
@{
	ViewData[""Title""] = ""Create"";
	<hr />
	<div class=""row"">
		<div class=""col-md-4"">
			<form method=""post"">
				<div class=""form-group"">
					<label asp-for=""Movie.Title"" class=""control-label""></label>
					<input asp-for=""Movie.Title"" class=""form-control"" />
					<span asp-validation-for=""Movie.Title"" class=""text-danger""></span>
				</div>
			</form>
		</div>
	</div>
}
",
tabSize: 4, // Due to a bug in the HTML formatter, this needs to be 4
insertSpaces: false,
fileKind: FileKinds.Legacy);
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1273468/")]
        public async Task FormatHtmlWithTabs2(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@page ""/""

 <hr />
 <div class=""row"">
  <div class=""col-md-4"">
   <form method=""post"">
    <div class=""form-group"">
     <label asp-for=""Movie.Title"" class=""control-label""></label>
     <input asp-for=""Movie.Title"" class=""form-control"" />
     <span asp-validation-for=""Movie.Title"" class=""text-danger""></span>
    </div>
   </form>
  </div>
 </div>
",
expected: @"@page ""/""

<hr />
<div class=""row"">
	<div class=""col-md-4"">
		<form method=""post"">
			<div class=""form-group"">
				<label asp-for=""Movie.Title"" class=""control-label""></label>
				<input asp-for=""Movie.Title"" class=""form-control"" />
				<span asp-validation-for=""Movie.Title"" class=""text-danger""></span>
			</div>
		</form>
	</div>
</div>
",
tabSize: 4, // Due to a bug in the HTML formatter, this needs to be 4
insertSpaces: false,
fileKind: FileKinds.Legacy);
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/30382")]
        public async Task FormatNestedComponents(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
<CascadingAuthenticationState>
<Router AppAssembly=""@typeof(Program).Assembly"">
    <Found Context=""routeData"">
        <RouteView RouteData=""@routeData"" DefaultLayout=""@typeof(MainLayout)"" />
    </Found>
    <NotFound>
        <LayoutView Layout=""@typeof(MainLayout)"">
            <p>Sorry, there's nothing at this address.</p>

            @if (true)
                    {
                        <strong></strong>
                }
        </LayoutView>
    </NotFound>
</Router>
</CascadingAuthenticationState>
",
expected: @"
<CascadingAuthenticationState>
    <Router AppAssembly=""@typeof(Program).Assembly"">
        <Found Context=""routeData"">
            <RouteView RouteData=""@routeData"" DefaultLayout=""@typeof(MainLayout)"" />
        </Found>
        <NotFound>
            <LayoutView Layout=""@typeof(MainLayout)"">
                <p>Sorry, there's nothing at this address.</p>

                @if (true)
                {
                    <strong></strong>
                }
            </LayoutView>
        </NotFound>
    </Router>
</CascadingAuthenticationState>
");
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/30382")]
        public async Task FormatNestedComponents2(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
<GridTable>
<ChildContent>
<GridRow>
<ChildContent>
<GridCell>
<ChildContent>
<strong></strong>
@if (true)
{
<strong></strong>
}
<strong></strong>
</ChildContent>
</GridCell>
</ChildContent>
</GridRow>
</ChildContent>
</GridTable>
",
expected: @"
<GridTable>
    <ChildContent>
        <GridRow>
            <ChildContent>
                <GridCell>
                    <ChildContent>
                        <strong></strong>
                        @if (true)
                        {
                            <strong></strong>
                        }
                        <strong></strong>
                    </ChildContent>
                </GridCell>
            </ChildContent>
        </GridRow>
    </ChildContent>
</GridTable>
", tagHelpers: GetComponents());
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/30382")]
        public async Task FormatNestedComponents2_Range(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
<GridTable>
<ChildContent>
<GridRow>
<ChildContent>
<GridCell>
<ChildContent>
<strong></strong>
@if (true)
{
[|<strong></strong>|]
}
<strong></strong>
</ChildContent>
</GridCell>
</ChildContent>
</GridRow>
</ChildContent>
</GridTable>
",
expected: @"
<GridTable>
<ChildContent>
<GridRow>
<ChildContent>
<GridCell>
<ChildContent>
<strong></strong>
@if (true)
{
                            <strong></strong>
}
<strong></strong>
</ChildContent>
</GridCell>
</ChildContent>
</GridRow>
</ChildContent>
</GridTable>
", tagHelpers: GetComponents());
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/29645")]
        public async Task FormatHtmlInIf(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@if (true)
{
    <p><em>Loading...</em></p>
}
else
{
    <table class=""table"">
        <thead>
            <tr>
        <th>Date</th>
        <th>Temp. (C)</th>
        <th>Temp. (F)</th>
        <th>Summary</th>
            </tr>
        </thead>
    </table>
}
",
expected: @"@if (true)
{
    <p><em>Loading...</em></p>
}
else
{
    <table class=""table"">
        <thead>
            <tr>
                <th>Date</th>
                <th>Temp. (C)</th>
                <th>Temp. (F)</th>
                <th>Summary</th>
            </tr>
        </thead>
    </table>
}
");
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/29645")]
        public async Task FormatHtmlInIf_Range(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@if (true)
{
    <p><em>Loading...</em></p>
}
else
{
    <table class=""table"">
        <thead>
            <tr>
[|      <th>Date</th>
        <th>Temp. (C)</th>
        <th>Temp. (F)</th>
        <th>Summary</th>|]
            </tr>
        </thead>
    </table>
}
",
expected: @"
@if (true)
{
    <p><em>Loading...</em></p>
}
else
{
    <table class=""table"">
        <thead>
            <tr>
                <th>Date</th>
                <th>Temp. (C)</th>
                <th>Temp. (F)</th>
                <th>Summary</th>
            </tr>
        </thead>
    </table>
}
");
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/razor-tooling/issues/5749")]
        public async Task FormatRenderFragmentInCSharpCodeBlock(bool useSourceTextDiffer)
        {
            // Sadly the first thing the HTML formatter does with this input
            // is put a newline after the @, which means <SurveyPrompt /> won't be
            // seen as a component any more, so we have to turn off our validation,
            // or the test fails before we have a chance to fix the formatting.
            FormattingContext.SkipValidateComponents = true;

            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code
{
    public void DoStuff(RenderFragment renderFragment)
    {
        renderFragment(@<SurveyPrompt Title=""Foo"" />);

        @* comment *@
<div></div>

        @* comment *@<div></div>
    }
}
",
expected: @"@code
{
    public void DoStuff(RenderFragment renderFragment)
    {
        renderFragment(@<SurveyPrompt Title=""Foo"" />);

        @* comment *@
        <div></div>

        @* comment *@
        <div></div>
    }
}
",
tagHelpers: GetSurveyPrompt());
        }

        private IReadOnlyList<TagHelperDescriptor> GetSurveyPrompt()
        {
            AdditionalSyntaxTrees.Add(Parse(@"
using Microsoft.AspNetCore.Components;
namespace Test
{
    public class SurveyPrompt : ComponentBase
    {
        [Parameter]
        public string Title { get; set; }
    }
}
"));

            var generated = CompileToCSharp("SurveyPrompt.razor", string.Empty, throwOnFailure: false, fileKind: FileKinds.Component);
            var tagHelpers = generated.CodeDocument.GetTagHelperContext().TagHelpers;
            return tagHelpers;
        }

        private IReadOnlyList<TagHelperDescriptor> GetComponents()
        {
            AdditionalSyntaxTrees.Add(Parse(@"
using Microsoft.AspNetCore.Components;
namespace Test
{
    public class GridTable : ComponentBase
    {
        [Parameter]
        public RenderFragment ChildContent { get; set; }
    }

    public class GridRow : ComponentBase
    {
        [Parameter]
        public RenderFragment ChildContent { get; set; }
    }

    public class GridCell : ComponentBase
    {
        [Parameter]
        public RenderFragment ChildContent { get; set; }
    }
}
"));

            var generated = CompileToCSharp("Test.razor", string.Empty, throwOnFailure: false, fileKind: FileKinds.Component);
            var tagHelpers = generated.CodeDocument.GetTagHelperContext().TagHelpers;
            return tagHelpers;
        }
    }
}
