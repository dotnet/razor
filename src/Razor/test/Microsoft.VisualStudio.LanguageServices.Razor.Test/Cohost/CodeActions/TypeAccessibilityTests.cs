// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost.CodeActions;

public class TypeAccessibilityTests(ITestOutputHelper testOutputHelper) : CohostCodeActionsEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task FixCasing()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <Edit[||]form></Editform>
                """,
            expected: """
                <div></div>

                <EditForm></EditForm>
                """,
            codeActionName: LanguageServerConstants.CodeActions.FullyQualify);
    }

    [Fact]
    public async Task FullyQualify()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <Section[||]Outlet></SectionOutlet>
                """,
            expected: """
                <div></div>

                <Microsoft.AspNetCore.Components.Sections.SectionOutlet></Microsoft.AspNetCore.Components.Sections.SectionOutlet>
                """,
            codeActionName: LanguageServerConstants.CodeActions.FullyQualify);
    }

    [Fact]
    public async Task AddUsing()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <Section[||]Outlet></SectionOutlet>
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Sections
                <div></div>

                <SectionOutlet></SectionOutlet>
                """,
            codeActionName: LanguageServerConstants.CodeActions.AddUsing);
    }

    [Fact]
    public async Task AddUsing_FixTypo()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <Section[||]outlet></Sectionoutlet>
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Sections
                <div></div>

                <SectionOutlet></SectionOutlet>
                """,
            codeActionName: LanguageServerConstants.CodeActions.AddUsing);
    }
}
