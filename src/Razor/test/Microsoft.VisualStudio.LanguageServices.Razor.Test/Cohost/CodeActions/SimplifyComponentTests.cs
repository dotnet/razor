// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost.CodeActions;

public class SimplifyComponentTests(ITestOutputHelper testOutputHelper) : CohostCodeActionsEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task SimplifyFullyQualifiedComponent_AddsUsing()
    {
        await VerifyCodeActionAsync(
            input: """
                <Microsoft.AspNetCore.Components.Authorization.Aut[||]horizeRouteView />
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Authorization

                <AuthorizeRouteView />
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent,
            additionalFiles: [
                (FilePath("AuthorizeRouteView.razor"), """
                    <div>
                        Hello World
                    </div>
                    """)]);
    }

    [Fact]
    public async Task SimplifyFullyQualifiedComponent_UsingAlreadyExists()
    {
        await VerifyCodeActionAsync(
            input: """
                @using Microsoft.AspNetCore.Components.Authorization

                <Microsoft.AspNetCore.Components.Authorization.Aut[||]horizeRouteView />
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Authorization

                <AuthorizeRouteView />
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent,
            additionalFiles: [
                (FilePath("AuthorizeRouteView.razor"), """
                    <div>
                        Hello World
                    </div>
                    """)]);
    }

    [Fact]
    public async Task SimplifyFullyQualifiedComponent_WithEndTag()
    {
        await VerifyCodeActionAsync(
            input: """
                <Microsoft.AspNetCore.Components.Authorization.Auth[||]orizeRouteView>
                    <div>Content</div>
                </Microsoft.AspNetCore.Components.Authorization.AuthorizeRouteView>
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Authorization

                <AuthorizeRouteView>
                    <div>Content</div>
                </AuthorizeRouteView>
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent,
            additionalFiles: [
                (FilePath("AuthorizeRouteView.razor"), """
                    <div>
                        Hello World
                    </div>
                    
                    @code {
                        [Parameter]
                        public RenderFragment? ChildContent { get; set; }
                    }
                    """)]);
    }

    [Fact]
    public async Task SimplifyFullyQualifiedComponent_MultipleInstances()
    {
        await VerifyCodeActionAsync(
            input: """
                <Microsoft.AspNetCore.Components.Authorization.Auth[||]orizeRouteView />

                <Microsoft.AspNetCore.Components.Authorization.AuthorizeRouteView>
                    <div>Content</div>
                </Microsoft.AspNetCore.Components.Authorization.AuthorizeRouteView>
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Authorization

                <AuthorizeRouteView />

                <AuthorizeRouteView>
                    <div>Content</div>
                </AuthorizeRouteView>
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent,
            additionalFiles: [
                (FilePath("AuthorizeRouteView.razor"), """
                    <div>
                        Hello World
                    </div>
                    
                    @code {
                        [Parameter]
                        public RenderFragment? ChildContent { get; set; }
                    }
                    """)]);
    }

    [Fact]
    public async Task DoNotOfferOnUnqualifiedComponent()
    {
        await VerifyCodeActionAsync(
            input: """
                <Compone[||]nt />
                """,
            expected: null,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent,
            additionalFiles: [
                (FilePath("Component.razor"), """
                    <div>
                        Hello World
                    </div>
                    """)]);
    }

    [Fact]
    public async Task DoNotOfferOnHtmlTag()
    {
        await VerifyCodeActionAsync(
            input: """
                <di[||]v></div>
                """,
            expected: null,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent);
    }

    [Fact]
    public async Task DoNotOfferOnNonExistentComponent()
    {
        await VerifyCodeActionAsync(
            input: """
                <{|RZ10012:Not$$AComponent.Sub.Namespace|}></NotAComponent.Sub.Namespace>
                """,
            expected: null,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent);
    }

    [Fact]
    public async Task SimplifyWithNestedNamespace()
    {
        await VerifyCodeActionAsync(
            input: """
                <My.Custom.Namespace.Compo[||]nent />
                """,
            expected: """
                @using My.Custom.Namespace

                <Component />
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent,
            additionalFiles: [
                (FilePath("Component.razor"), """
                    @namespace My.Custom.Namespace
                    
                    <div>
                        Hello World
                    </div>
                    """)]);
    }

    [Fact]
    public async Task SimplifyComponentWithAttributes()
    {
        await VerifyCodeActionAsync(
            input: """
                <Microsoft.AspNetCore.Components.Authorization.Auth[||]orizeRouteView Attribute="Value" AnotherAttr="Test" />
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Authorization

                <AuthorizeRouteView Attribute="Value" AnotherAttr="Test" />
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent,
            additionalFiles: [
                (FilePath("AuthorizeRouteView.razor"), """
                    <div>
                        Hello World
                    </div>
                    
                    @code {
                        [Parameter]
                        public string? Attribute { get; set; }

                        [Parameter]
                        public string? AnotherAttr { get; set; }
                    }
                    """)]);
    }

    [Fact]
    public async Task SimplifyWithExistingUsingDirectives()
    {
        await VerifyCodeActionAsync(
            input: """
                @using System
                @using System.Collections.Generic

                <Microsoft.AspNetCore.Components.Authorization.Auth[||]orizeRouteView />
                """,
            expected: """
                @using System
                @using System.Collections.Generic
                @using Microsoft.AspNetCore.Components.Authorization

                <AuthorizeRouteView />
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent,
            additionalFiles: [
                (FilePath("AuthorizeRouteView.razor"), """
                    <div>
                        Hello World
                    </div>
                    """)]);
    }
}
