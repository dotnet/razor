// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

public class CodeActionFormattingTest(ITestOutputHelper testOutput) : FormattingTestBase(testOutput)
{
    [Fact]
    public async Task AddDebuggerDisplay()
    {
        await RunCodeActionFormattingTestAsync(
input: @"
@functions {
    class Goo$$
    {
        
    }
}
",
codeActionEdits: new[]
{
Edit(7, 6, 7, 6, "System.Diagnostics;\r\nusing "),
Edit(69, 0, 69, 8, ""),
Edit(71, 34, 72, 7, "\r\n\r\n        [DebuggerDisplay($\"{{{nameof(GetDebuggerDisplay)}(),nq}}\")]"),
Edit(73, 0, 73, 4, "        "),
Edit(74, 5, 74, 5, "\r\n            private string GetDebuggerDisplay()\r\n            {"),
Edit(75, 0, 75, 0, "                return ToString();\r\n            }\r\n"),
Edit(75, 8, 76, 4, "")
},
expected: @"@using System.Diagnostics

@functions {
    [DebuggerDisplay($""{{{nameof(GetDebuggerDisplay)}(),nq}}"")]  
    class Goo
    {
        private string GetDebuggerDisplay()
        {
            return ToString();
        }
    }
}
");
    }

    [Fact]
    public async Task GenerateConstructor()
    {
        await RunCodeActionFormattingTestAsync(
input: @"
@functions {
    class Goo$$
    {
        
    }
}
",
codeActionEdits: new[]
{
Edit(69, 0, 69, 8, ""),
Edit(71, 33, 71, 33, "\r\n\r\n        class Goo"),
Edit(72, 0, 72, 12, "        {"),
Edit(73, 0, 73, 9, "            public"),
Edit(73, 13, 73, 13, "()"),
Edit(74, 0, 74, 4, "            "),
Edit(75, 0, 75, 4, "            }"),
Edit(76, 0, 76, 4, "        "),
},
expected: @"
@functions {
    class Goo
    {
        public Goo()
        {
        }    
    }
}
");
    }

    [Fact]
    public async Task OverrideCompletion()
    {
        await RunCodeActionFormattingTestAsync(
input: @"
@functions {
    override $$
}
",
codeActionEdits: new[]
{
Edit(67, 0, 74, 0, "        {\r\n        }\r\n#pragma warning restore 1998\r\n#nullable restore\r\n#line 2 \"e:/Scratch/BlazorApp13/BlazorApp13/Client/Pages/Test.razor\"\r\n\r\n        protected override void OnAfterRender(bool firstRender)\r\n        {\r\n            base.OnAfterRender(firstRender);/*$0*/\r\n        }\r\n"),
},
expected: @"
@functions {
    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);/*$0*/
    }
}
");
    }
}
