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
Edit(55, 0, 55, 8, ""),
Edit(57, 34, 58, 7, "\r\n\r\n        [DebuggerDisplay($\"{{{nameof(GetDebuggerDisplay)}(),nq}}\")]"),
Edit(59, 0, 59, 4, "        "),
Edit(60, 5, 60, 5, "\r\n            private string GetDebuggerDisplay()\r\n            {"),
Edit(61, 0, 61, 0, "                return ToString();\r\n            }\r\n"),
Edit(61, 8, 62, 4, "")
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
Edit(55, 0, 55, 8, ""),
Edit(57, 33, 57, 33, "\r\n\r\n        class Goo"),
Edit(58, 0, 58, 12, "        {"),
Edit(59, 0, 59, 9, "            public"),
Edit(59, 13, 59, 13, "()"),
Edit(60, 0, 60, 4, "            "),
Edit(61, 0, 61, 4, "            }"),
Edit(62, 0, 62, 4, "        "),
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
Edit(53, 0, 60, 0, "        {\r\n        }\r\n#pragma warning restore 1998\r\n#nullable restore\r\n#line 2 \"e:/Scratch/BlazorApp13/BlazorApp13/Client/Pages/Test.razor\"\r\n\r\n        protected override void OnAfterRender(bool firstRender)\r\n        {\r\n            base.OnAfterRender(firstRender);/*$0*/\r\n        }\r\n"),
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
