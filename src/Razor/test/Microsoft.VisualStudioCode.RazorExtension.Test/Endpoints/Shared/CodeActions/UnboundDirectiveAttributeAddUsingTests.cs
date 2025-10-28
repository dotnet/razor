// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost.CodeActions;

public class UnboundDirectiveAttributeAddUsingTests(ITestOutputHelper testOutputHelper) : CohostCodeActionsEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task AddUsing_OnClick()
    {
        var input = """
            <button @on[||]click="HandleClick"></button>
            """;

        var expected = """
            @using Microsoft.AspNetCore.Components.Web
            <button @onclick="HandleClick"></button>
            """;

        await VerifyCodeActionAsync(input, expected, LanguageServerConstants.CodeActions.AddUsing);
    }

    [Fact]
    public async Task AddUsing_OnClick_WithExisting()
    {
        var input = """
            @using System

            <button @on[||]click="HandleClick"></button>
            """;

        var expected = """
            @using System
            @using Microsoft.AspNetCore.Components.Web

            <button @onclick="HandleClick"></button>
            """;

        await VerifyCodeActionAsync(input, expected, LanguageServerConstants.CodeActions.AddUsing);
    }

    [Fact]
    public async Task AddUsing_OnChange()
    {
        var input = """
            <input @on[||]change="HandleChange" />
            """;

        var expected = """
            @using Microsoft.AspNetCore.Components.Web
            <input @onchange="HandleChange" />
            """;

        await VerifyCodeActionAsync(input, expected, LanguageServerConstants.CodeActions.AddUsing);
    }

    [Fact]
    public async Task NoCodeAction_WhenNamespaceAlreadyPresent()
    {
        var input = """
            @using Microsoft.AspNetCore.Components.Web

            <button @on[||]click="HandleClick"></button>
            """;

        await VerifyCodeActionAsync(input, expected: null, LanguageServerConstants.CodeActions.AddUsing);
    }

    [Fact]
    public async Task NoCodeAction_WhenBoundAttribute()
    {
        var input = """
            @using Microsoft.AspNetCore.Components.Web

            <button @on[||]click="HandleClick"></button>
            """;

        await VerifyCodeActionAsync(input, expected: null, LanguageServerConstants.CodeActions.AddUsing);
    }

    [Fact]
    public async Task NoCodeAction_WhenNotOnDirectiveAttribute()
    {
        var input = """
            <button cl[||]ass="btn"></button>
            """;

        await VerifyCodeActionAsync(input, expected: null, LanguageServerConstants.CodeActions.AddUsing);
    }

    [Fact]
    public async Task AddUsing_Bind()
    {
        var input = """
            <input @bi[||]nd="value" />
            """;

        var expected = """
            @using Microsoft.AspNetCore.Components.Web
            <input @bind="value" />
            """;

        await VerifyCodeActionAsync(input, expected, LanguageServerConstants.CodeActions.AddUsing);
    }

    [Fact]
    public async Task AddUsing_BindValue()
    {
        var input = """
            <input @bind-va[||]lue="value" />
            """;

        var expected = """
            @using Microsoft.AspNetCore.Components.Web
            <input @bind-value="value" />
            """;

        await VerifyCodeActionAsync(input, expected, LanguageServerConstants.CodeActions.AddUsing);
    }
}
