﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    public class CSharpStatementBlockOnTypeFormattingTest : FormattingTestBase
    {
        public CSharpStatementBlockOnTypeFormattingTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public async Task CloseCurly_IfBlock_SingleLine()
        {
            await RunOnTypeFormattingTestAsync(
input: @"
@{
 if(true){$$}
}
",
expected: @"
@{
    if (true) { }
}
");
        }

        [Fact]
        public async Task CloseCurly_IfBlock_MultiLine()
        {
            await RunOnTypeFormattingTestAsync(
input: @"
@{
 if(true)
{
 $$}
}
",
expected: @"
@{
    if (true)
    {
    }
}
");
        }

        [Fact]
        public async Task CloseCurly_MultipleStatementBlocks()
        {
            await RunOnTypeFormattingTestAsync(
input: @"
<div>
    @{
      if(true) { }
    }
</div>

@{
 if(true)
{
 $$}
}
",
expected: @"
<div>
    @{
      if(true) { }
    }
</div>

@{
    if (true)
    {
    }
}
");
        }

        [Fact]
        public async Task Semicolon_Variable_SingleLine()
        {
            await RunOnTypeFormattingTestAsync(
input: @"
@{
 var x = 'foo'$$;
}
",
expected: @"
@{
    var x = 'foo';
}
");
        }

        [Fact]
        public async Task Semicolon_Variable_MultiLine()
        {
            await RunOnTypeFormattingTestAsync(
input: @"
@{
 var x = @""
foo""$$;
}
",
expected: @"
@{
    var x = @""
foo"";
}
");
        }
    }
}
