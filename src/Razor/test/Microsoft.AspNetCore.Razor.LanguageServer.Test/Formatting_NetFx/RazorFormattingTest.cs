// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting;

public class RazorFormattingTest(FormattingTestContext context, ITestOutputHelper testOutput) : FormattingTestBase(context, testOutput), IClassFixture<FormattingTestContext>
{
    [Fact]
    public async Task OnTypeFormatting_Enabled()
    {
        await RunOnTypeFormattingTestAsync(
            input: """
            @functions {
            	private int currentCount = 0;
            
            	private void IncrementCount (){
            		currentCount++;
            	}$$
            }
            """,
            expected: """
            @functions {
                private int currentCount = 0;
            
                private void IncrementCount()
                {
                    currentCount++;
                }
            }
            """,
            triggerCharacter: '}',
            razorLSPOptions: RazorLSPOptions.Default);
    }
}
