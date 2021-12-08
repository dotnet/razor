// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    public class RazorFormattingTest : FormattingTestBase
    {
        public RazorFormattingTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [CombinatorialData]
        public async Task CodeBlock_SpansMultipleLines(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code
        {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
",
expected: @"@code
{
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task CodeBlock_IndentedBlock_MaintainsIndent(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
<boo>
    @code
            {
        private int currentCount = 0;

        private void IncrementCount()
        {
            currentCount++;
        }
    }
</boo>
",
expected: @"
<boo>
    @code
    {
        private int currentCount = 0;

        private void IncrementCount()
        {
            currentCount++;
        }
    }
</boo>
");
        }

        [Theory]
        [CombinatorialData]
        public async Task CodeBlock_TooMuchWhitespace(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code        {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
",
expected: @"@code {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task CodeBlock_NonSpaceWhitespace(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code	{
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
",
expected: @"@code {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task CodeBlock_NoWhitespace(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@code{
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
",
expected: @"@code {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
");
        }

        [Theory]
        [CombinatorialData]
        public async Task FunctionsBlock_BraceOnNewLine(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions
        {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
",
expected: @"@functions
{
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
",
fileKind: FileKinds.Legacy);
        }

        [Theory]
        [CombinatorialData]
        public async Task FunctionsBlock_TooManySpaces(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@functions        {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
",
expected: @"@functions {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }
}
",
fileKind: FileKinds.Legacy);
        }

        [Theory]
        [CombinatorialData]
        public async Task Layout(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@layout    MyLayout
",
expected: @"@layout MyLayout
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Inherits(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@inherits    MyBaseClass
",
expected: @"@inherits MyBaseClass
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Implements(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@implements    IDisposable
",
expected: @"@implements IDisposable
");
        }

        [Theory]
        [CombinatorialData]
        public async Task PreserveWhitespace(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@preservewhitespace    true
",
expected: @"@preservewhitespace true
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Inject(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@inject    MyClass     myClass
",
expected: @"@inject MyClass myClass
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Inject_TrailingWhitespace(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@inject    MyClass     myClass
",
expected: @"@inject MyClass myClass
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Attribute(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@attribute     [Obsolete(   ""asdf""   , error:    false)]
",
expected: @"@attribute [Obsolete(""asdf"", error: false)]
");
        }

        [Theory]
        [CombinatorialData]
        public async Task Model(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@model    MyModel
",
expected: @"@model MyModel
",
            fileKind: FileKinds.Legacy);
        }

        [Theory]
        [CombinatorialData]
        public async Task Page(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@page    ""MyPage""
",
expected: @"@page ""MyPage""
",
            fileKind: FileKinds.Legacy);
        }

        // Regression prevention tests:
        [Theory]
        [CombinatorialData]
        public async Task Using(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@using   System;
",
expected: @"@using System;
");
        }

        [Theory]
        [CombinatorialData]
        public async Task UsingStatic(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@using  static   System.Math;
",
expected: @"@using static System.Math;
");
        }

        [Theory]
        [CombinatorialData]
        public async Task UsingAlias(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@using  M   =    System.Math;
",
expected: @"@using M = System.Math;
");
        }

        [Theory]
        [CombinatorialData]
        public async Task TagHelpers(bool useSourceTextDiffer)
        {
            await RunFormattingTestAsync(useSourceTextDiffer: useSourceTextDiffer,
input: @"
@addTagHelper    *,    Microsoft.AspNetCore.Mvc.TagHelpers
@removeTagHelper    *,     Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper    ""*,  Microsoft.AspNetCore.Mvc.TagHelpers""
@removeTagHelper    ""*,  Microsoft.AspNetCore.Mvc.TagHelpers""
@tagHelperPrefix    th:
",
expected: @"@addTagHelper    *,    Microsoft.AspNetCore.Mvc.TagHelpers
@removeTagHelper    *,     Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper    ""*,  Microsoft.AspNetCore.Mvc.TagHelpers""
@removeTagHelper    ""*,  Microsoft.AspNetCore.Mvc.TagHelpers""
@tagHelperPrefix    th:
",
            fileKind: FileKinds.Legacy);
        }
    }
}
