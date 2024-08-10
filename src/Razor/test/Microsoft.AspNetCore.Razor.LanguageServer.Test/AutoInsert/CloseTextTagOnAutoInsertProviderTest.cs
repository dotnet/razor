// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.AutoInsert;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert;

public class CloseTextTagOnAutoInsertProviderTest(ITestOutputHelper testOutput) : RazorOnAutoInsertProviderTestBase(testOutput)
{
    [Fact]
    public async Task OnTypeCloseAngle_ClosesTextTagAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
@{
    <text>$$
}
",
expected: @"
@{
    <text>$0</text>
}
");
    }

    [Fact]
    public async Task OnTypeCloseAngle_OutsideRazorBlock_DoesNotCloseTextTagAsync()
    {
        await RunAutoInsertTestAsync(
input: @"
    <text>$$
",
expected: @"
    <text>
");
    }

    internal override IOnAutoInsertProvider CreateProvider() =>
        new CloseTextTagOnAutoInsertProvider();
}
