// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    public class CodeDirectiveFormattingTest : FormattingTestBase
    {
        public CodeDirectiveFormattingTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [CombinatorialData]
        public async Task FormatsCodeBlockDirective(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code {
 public class Foo{}
        public interface Bar {
}
}
",
expected: @"@code {
    public class Foo { }
    public interface Bar
    {
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Formats_MultipleBlocksInADirective(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@{
void Method(){
var x = ""foo"";
@(DateTime.Now)
    <p></p>
var y= ""fooo"";
}
}
<div>
        </div>
",
expected: @"@{
    void Method()
    {
        var x = ""foo"";
        @(DateTime.Now)
        <p></p>
        var y = ""fooo"";
    }
}
<div>
</div>
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Formats_NonCodeBlockDirectives(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@{
var x = ""foo"";
}
<div>
        </div>
",
expected: @"@{
    var x = ""foo"";
}
<div>
</div>
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Formats_CodeBlockDirectiveWithMarkup_NonBraced(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions {
 public class Foo{
void Method() { var x = ""t""; <div></div> var y = ""t"";}
}
}
",
expected: @"@functions {
    public class Foo
    {
        void Method()
        {
            var x = ""t"";
            <div></div>
            var y = ""t"";
        }
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Formats_CodeBlockDirectiveWithMarkup(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions {
 public class Foo{
void Method() { <div></div> }
}
}
",
expected: @"@functions {
    public class Foo
    {
        void Method()
        {
            <div></div>
        }
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Formats_CodeBlockDirectiveWithImplicitExpressions(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code {
 public class Foo{
void Method() { @DateTime.Now }
    }
}
",
expected: @"@code {
    public class Foo
    {
        void Method()
        {
            @DateTime.Now
        }
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task DoesNotFormat_CodeBlockDirectiveWithExplicitExpressions(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions {
 public class Foo{
void Method() { @(DateTime.Now) }
    }
}
",
expected: @"@functions {
    public class Foo
    {
        void Method()
        {
            @(DateTime.Now)
        }
    }
}
",
fileKind: FileKinds.Legacy);
        }

        [Theory]
        [CombinatorialData]
        public async Task DoesNotFormat_SectionDirectiveBlock(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions {
 public class Foo{
void Method() {  }
    }
}

@section Scripts {
<script></script>
}
",
expected: @"@functions {
    public class Foo
    {
        void Method() { }
    }
}

@section Scripts {
<script></script>
}
",
fileKind: FileKinds.Legacy);
        }

        [Theory]
        [CombinatorialData]
        public async Task Formats_CodeBlockDirectiveWithRazorComments(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions {
 public class Foo{
@* This is a Razor Comment *@
void Method() {  }
}
}
",
expected: @"@functions {
    public class Foo
    {
        @* This is a Razor Comment *@
        void Method() { }
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Formats_CodeBlockDirectiveWithRazorStatements(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions {
 public class Foo{
@* This is a Razor Comment *@
    }
}
",
expected: @"@functions {
    public class Foo
    {
        @* This is a Razor Comment *@
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task DoesNotFormat_CodeBlockDirective_NotInSelectedRange(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
[|<div>Foo</div>|]
@functions {
 public class Foo{}
        public interface Bar {
}
}
",
expected: @"
<div>Foo</div>
@functions {
 public class Foo{}
        public interface Bar {
}
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task OnlyFormatsWithinRange(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions {
 public class Foo{}
        [|public interface Bar {
}|]
}
",
expected: @"
@functions {
 public class Foo{}
    public interface Bar
    {
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task MultipleCodeBlockDirectives(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions {
 public class Foo{}
        public interface Bar {
}
}
Hello World
@functions {
      public class Baz    {
          void Method ( )
          { }
          }
}
",
expected: @"@functions {
    public class Foo { }
    public interface Bar
    {
    }
}
Hello World
@functions {
    public class Baz
    {
        void Method()
        { }
    }
}
",
fileKind: FileKinds.Legacy);
        }

        [Theory]
        [CombinatorialData]
        public async Task MultipleCodeBlockDirectives2(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
Hello World
@code {
public class HelloWorld
{
}
}

@functions{

 public class Bar {}
}
",
expected: @"Hello World
@code {
    public class HelloWorld
    {
    }
}

@functions {

    public class Bar { }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task CodeOnTheSameLineAsCodeBlockDirectiveStart(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions {public class Foo{
}
}
",
expected: @"@functions {
    public class Foo
    {
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task CodeOnTheSameLineAsCodeBlockDirectiveEnd(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions {
public class Foo{
}}
",
expected: @"@functions {
    public class Foo
    {
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task SingleLineCodeBlockDirective(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions {public class Foo{}
}
",
expected: @"@functions {
    public class Foo { }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task IndentsCodeBlockDirectiveStart(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
Hello World
     @functions {public class Foo{}
}
",
expected: @"Hello World
@functions {
    public class Foo { }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task IndentsCodeBlockDirectiveEnd(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
 @functions {
public class Foo{}
     }
",
expected: @"@functions {
    public class Foo { }
     }
");
        }

        [Theory]
        [CombinatorialData]
        public async Task ComplexCodeBlockDirective(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@using System.Buffers
@functions{
     public class Foo
            {
                public Foo()
                {
                    var arr = new string[ ] { ""One"", ""two"",""three"" };
                    var str = @""
This should
not
be indented.
"";
                }
public int MyProperty { get
{
return 0 ;
} set {} }

void Method(){

}
                    }
}
",
expected: @"@using System.Buffers
@functions {
    public class Foo
    {
        public Foo()
        {
            var arr = new string[] { ""One"", ""two"", ""three"" };
            var str = @""
This should
not
be indented.
"";
        }
        public int MyProperty
        {
            get
            {
                return 0;
            }
            set { }
        }

        void Method()
        {

        }
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task CodeBlockDirective_UseTabs(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code {
 public class Foo{}
        void Method(  ) {
}
}
",
expected: @"@code {
	public class Foo { }
	void Method()
	{
	}
}
",
insertSpaces: false);

        }
        [Theory]
        [CombinatorialData]
        public async Task CodeBlockDirective_UseTabsWithTabSize8_HTML(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code {
 public class Foo{}
        void Method(  ) {<div></div>
}
}
",
expected: @"@code {
	public class Foo { }
	void Method()
	{
		<div></div>
	}
}
",
tabSize: 8,
insertSpaces: false);
        }

        [Theory]
        [CombinatorialData]
        public async Task CodeBlockDirective_UseTabsWithTabSize8(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code {
 public class Foo{}
        void Method(  ) {
}
}
",
expected: @"@code {
	public class Foo { }
	void Method()
	{
	}
}
",
tabSize: 8,
insertSpaces: false);
        }

        [Theory]
        [CombinatorialData]
        public async Task CodeBlockDirective_WithTabSize3(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code {
 public class Foo{}
        void Method(  ) {
}
}
",
expected: @"@code {
   public class Foo { }
   void Method()
   {
   }
}
",
tabSize: 3);
        }

        [Theory]
        [CombinatorialData]
        public async Task CodeBlockDirective_WithTabSize8(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code {
 public class Foo{}
        void Method(  ) {
}
}
",
expected: @"@code {
        public class Foo { }
        void Method()
        {
        }
}
",
tabSize: 8);
        }

        [Theory]
        [CombinatorialData]
        public async Task CodeBlockDirective_WithTabSize12(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code {
 public class Foo{}
        void Method(  ) {
}
}
",
expected: @"@code {
            public class Foo { }
            void Method()
            {
            }
}
",
tabSize: 12);
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/27102")]
        public async Task CodeBlock_SemiColon_SingleLine(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
<div></div>
@{ Debugger.Launch()$$;}
<div></div>
",
expected: @"
<div></div>
@{
    Debugger.Launch();
}
<div></div>
");
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/29837")]
        public async Task CodeBlock_NestedComponents(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code {
    private WeatherForecast[] forecasts;

    protected override async Task OnInitializedAsync()
    {
        <Counter>
            @{
                    var t = DateTime.Now;
                    t.ToString();
                }
            </Counter>
        forecasts = await ForecastService.GetForecastAsync(DateTime.Now);
    }
}
",
expected: @"@code {
    private WeatherForecast[] forecasts;

    protected override async Task OnInitializedAsync()
    {
        <Counter>
            @{
                var t = DateTime.Now;
                t.ToString();
            }
        </Counter>
        forecasts = await ForecastService.GetForecastAsync(DateTime.Now);
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/34320")]
        public async Task CodeBlock_ObjectCollectionArrayInitializers(bool useSourceTextDiffer)
        {
            // The C# Formatter doesn't touch these types of initializers, so nor to we. This test
            // just verifies we don't regress things and start moving code around.
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code {
    public List<object> AList = new List<object>()
    {
        new
        {
            Name = ""One"",
            Goo = new
            {
                First = 1,
                Second = 2
            },
            Bar = new string[] {
                ""Hello"",
                ""There""
            }
        }
    };
}
",
expected: @"@code {
    public List<object> AList = new List<object>()
    {
        new
        {
            Name = ""One"",
            Goo = new
            {
                First = 1,
                Second = 2
            },
            Bar = new string[] {
                ""Hello"",
                ""There""
            }
        }
    };
}
");
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/4498")]
        public async Task IfBlock_TopLevel(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
        @if (true)
{
}
",
expected: @"@if (true)
{
}
");
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/4498")]
        public async Task IfBlock_TopLevel_WithOtherCode(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@{
    // foo
}

        @if (true)
{
}
",
expected: @"@{
    // foo
}

@if (true)
{
}
");
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/4498")]
        public async Task IfBlock_Nested(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
<div>
        @if (true)
{
}
</div>
",
expected: @"
<div>
    @if (true)
    {
    }
</div>
");
        }
    }
}
