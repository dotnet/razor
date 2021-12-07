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
    public class CodeDirectiveFormattingTest : FormattingTestBase
    {
        internal override bool UseTwoPhaseCompilation => true;

        internal override bool DesignTime => true;

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
        public async Task Strings(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions{
private string str1 = ""hello world"";
private string str2 = $""hello world"";
private string str3 = @""hello world"";
private string str4 = $@""hello world"";
private string str5 = @""
    One
        Two
            Three
"";
private string str6 = $@""
    One
        Two
            Three
"";
// This looks wrong, but matches what the C# formatter does. Try it and see!
private string str7 = ""One"" +
    ""Two"" +
        ""Three"" +
"""";
}
",
expected: @"@functions {
    private string str1 = ""hello world"";
    private string str2 = $""hello world"";
    private string str3 = @""hello world"";
    private string str4 = $@""hello world"";
    private string str5 = @""
    One
        Two
            Three
"";
    private string str6 = $@""
    One
        Two
            Three
"";
    // This looks wrong, but matches what the C# formatter does. Try it and see!
    private string str7 = ""One"" +
        ""Two"" +
            ""Three"" +
    """";
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
            // The C# Formatter doesn't touch these types of initializers, so nor do we. This test
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
        [WorkItem("https://github.com/dotnet/razor-tooling/issues/5618")]
        public async Task CodeBlock_EmptyObjectCollectionInitializers(bool useSourceTextDiffer)
        {
            // The C# Formatter _does_ touch these types of initializers if they're empty. Who knew ¯\_(ツ)_/¯
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code {
    public void Foo()
    {
        SomeMethod(new List<string>()
            {

            });

        SomeMethod(new Exception
            {

            });
    }
}
",
expected: @"@code {
    public void Foo()
    {
        SomeMethod(new List<string>()
        {

        });

        SomeMethod(new Exception
        {

        });
    }
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

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/razor-tooling/issues/5648")]
        public async Task GenericComponentWithCascadingTypeParameter(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@page ""/counter""

@if(true)
    {
                // indented
        }

<TestGeneric Items=""_items"">
    @foreach (var v in System.Linq.Enumerable.Range(1, 10))
        {
            <div></div>
        }
    </TestGeneric>

@if(true)
    {
                // indented
            }

@code
    {
    private IEnumerable<int> _items = new[] { 1, 2, 3, 4, 5 };
}",
expected: @"@page ""/counter""

@if (true)
{
    // indented
}

<TestGeneric Items=""_items"">
    @foreach (var v in System.Linq.Enumerable.Range(1, 10))
    {
        <div></div>
    }
</TestGeneric>

@if (true)
{
    // indented
}

@code
{
    private IEnumerable<int> _items = new[] { 1, 2, 3, 4, 5 };
}",
tagHelpers: GetComponentWithCascadingTypeParameter());
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/razor-tooling/issues/5648")]
        public async Task GenericComponentWithCascadingTypeParameter_Nested(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@page ""/counter""

<TestGeneric Items=""_items"">
    @foreach (var v in System.Linq.Enumerable.Range(1, 10))
        {
            <div></div>
        }
<TestGeneric Items=""_items"">
    @foreach (var v in System.Linq.Enumerable.Range(1, 10))
        {
            <div></div>
        }
    </TestGeneric>
    </TestGeneric>

@code
    {
    private IEnumerable<int> _items = new[] { 1, 2, 3, 4, 5 };
}",
expected: @"@page ""/counter""

<TestGeneric Items=""_items"">
    @foreach (var v in System.Linq.Enumerable.Range(1, 10))
    {
        <div></div>
    }
    <TestGeneric Items=""_items"">
        @foreach (var v in System.Linq.Enumerable.Range(1, 10))
        {
            <div></div>
        }
    </TestGeneric>
</TestGeneric>

@code
{
    private IEnumerable<int> _items = new[] { 1, 2, 3, 4, 5 };
}",
tagHelpers: GetComponentWithCascadingTypeParameter());
        }

        [Theory]
        [CombinatorialData]
        [WorkItem("https://github.com/dotnet/razor-tooling/issues/5648")]
        public async Task GenericComponentWithCascadingTypeParameter_MultipleParameters(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@page ""/counter""

<TestGenericTwo Items=""_items"" ItemsTwo=""_items2"">
    @foreach (var v in System.Linq.Enumerable.Range(1, 10))
        {
            <div></div>
        }
    </TestGenericTwo>

@code
    {
    private IEnumerable<int> _items = new[] { 1, 2, 3, 4, 5 };
    private IEnumerable<long> _items2 = new long[] { 1, 2, 3, 4, 5 };
}",
expected: @"@page ""/counter""

<TestGenericTwo Items=""_items"" ItemsTwo=""_items2"">
    @foreach (var v in System.Linq.Enumerable.Range(1, 10))
    {
        <div></div>
    }
</TestGenericTwo>

@code
{
    private IEnumerable<int> _items = new[] { 1, 2, 3, 4, 5 };
    private IEnumerable<long> _items2 = new long[] { 1, 2, 3, 4, 5 };
}",
tagHelpers: GetComponentWithTwoCascadingTypeParameter());
        }

        [Theory]
        [CombinatorialData]
        public async Task Formats_MultilineExpressionAtStartOfBlock(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@{
    var x = DateTime
        .Now
        .ToString();
}
",
expected: @"@{
    var x = DateTime
        .Now
        .ToString();
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Formats_MultilineExpressionAfterWhitespaceAtStartOfBlock(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@{
    
        

    var x = DateTime
        .Now
        .ToString();
}
",
expected: @"@{



    var x = DateTime
        .Now
        .ToString();
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Formats_MultilineExpressionNotAtStartOfBlock(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@{
    //
    var x = DateTime
        .Now
        .ToString();
}
",
expected: @"@{
    //
    var x = DateTime
        .Now
        .ToString();
}
");
        }

        private IReadOnlyList<TagHelperDescriptor> GetComponentWithCascadingTypeParameter()
        {
            var input = @"
@using System.Collections.Generic
@using Microsoft.AspNetCore.Components
@typeparam TItem
@attribute [CascadingTypeParameter(nameof(TItem))]

<h3>TestGeneric</h3>

@code
{
    [Parameter] public IEnumerable<TItem> Items { get; set; }
    [Parameter] public RenderFragment ChildContent { get; set; }
}
            ";

            var generated = CompileToCSharp("TestGeneric.razor", input, throwOnFailure: true, fileKind: FileKinds.Component);
            var tagHelpers = generated.CodeDocument.GetTagHelperContext().TagHelpers;
            return tagHelpers;
        }

        private IReadOnlyList<TagHelperDescriptor> GetComponentWithTwoCascadingTypeParameter()
        {
            var input = @"
@using System.Collections.Generic
@using Microsoft.AspNetCore.Components
@typeparam TItem
@typeparam TItemTwo
@attribute [CascadingTypeParameter(nameof(TItem))]
@attribute [CascadingTypeParameter(nameof(TItemTwo))]

<h3>TestGeneric</h3>

@code
{
    [Parameter] public IEnumerable<TItem> Items { get; set; }
    [Parameter] public IEnumerable<TItemTwo> ItemsTwo { get; set; }
    [Parameter] public RenderFragment ChildContent { get; set; }
}
            ";

            var generated = CompileToCSharp("TestGenericTwo.razor", input, throwOnFailure: true, fileKind: FileKinds.Component);
            var tagHelpers = generated.CodeDocument.GetTagHelperContext().TagHelpers;
            return tagHelpers;
        }
    }
}
