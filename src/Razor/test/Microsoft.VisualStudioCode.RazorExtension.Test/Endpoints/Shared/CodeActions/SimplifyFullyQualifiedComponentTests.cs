// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost.CodeActions;

public class SimplifyFullyQualifiedComponentTests(ITestOutputHelper testOutputHelper) : CohostCodeActionsEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task SimplifyFullyQualifiedComponent_NoExistingUsing()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <Microsoft.AspNetCore.Components.Authorization.AuthorizeRoute[||]View />

                <div></div>
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Authorization
                <div></div>

                <AuthorizeRouteView />

                <div></div>
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent);
    }

    [Fact]
    public async Task SimplifyFullyQualifiedComponent_WithExistingUsing()
    {
        await VerifyCodeActionAsync(
            input: """
                @using Microsoft.AspNetCore.Components.Authorization

                <div></div>

                <Microsoft.AspNetCore.Components.Authorization.AuthorizeRoute[||]View />

                <div></div>
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Authorization

                <div></div>

                <AuthorizeRouteView />

                <div></div>
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent);
    }

    [Fact]
    public async Task SimplifyFullyQualifiedComponent_WithStartAndEndTags()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <Microsoft.AspNetCore.Components.Authorization.AuthorizeRoute[||]View>
                    <p>Content</p>
                </Microsoft.AspNetCore.Components.Authorization.AuthorizeRouteView>

                <div></div>
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Authorization
                <div></div>

                <AuthorizeRouteView>
                    <p>Content</p>
                </AuthorizeRouteView>

                <div></div>
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent);
    }

    [Fact]
    public async Task SimplifyFullyQualifiedComponent_WithAttributes()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <Microsoft.AspNetCore.Components.Authorization.AuthorizeRoute[||]View Resource="test" />

                <div></div>
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Authorization
                <div></div>

                <AuthorizeRouteView Resource="test" />

                <div></div>
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent);
    }

    [Fact]
    public async Task DoNotOfferOnSimpleComponent()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <Compo[||]nent />

                <div></div>
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
                <div></div>

                <d[||]iv>
                    Hello World
                </div>

                <div></div>
                """,
            expected: null,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent);
    }

    [Fact]
    public async Task DoNotOfferWhenDiagnosticPresent()
    {
        // When there are diagnostics on the start tag, we shouldn't offer the code action
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <{|RZ10012:NotACompo$$nent|} />

                <div></div>
                """,
            expected: null,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent);
    }

    [Fact]
    public async Task SimplifyFullyQualifiedComponent_NestedNamespace()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <MyCompany.MyApp.Custom[||]Component />

                <div></div>
                """,
            expected: """
                @using MyCompany.MyApp
                <div></div>

                <CustomComponent />

                <div></div>
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent,
            additionalFiles: [
                (FilePath("MyCompany/MyApp/CustomComponent.razor"), """
                    @namespace MyCompany.MyApp
                    
                    <div>
                        Custom Component
                    </div>
                    """)]);
    }

    [Fact]
    public async Task SimplifyFullyQualifiedComponent_MultipleOccurrences()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <Microsoft.AspNetCore.Components.Authorization.AuthorizeRoute[||]View />
                <Microsoft.AspNetCore.Components.Authorization.AuthorizeRouteView />

                <div></div>
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Authorization
                <div></div>

                <AuthorizeRouteView />
                <Microsoft.AspNetCore.Components.Authorization.AuthorizeRouteView />

                <div></div>
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent);
    }

    [Fact]
    public async Task SimplifyFullyQualifiedComponent_SelfClosingNoSpace()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <Microsoft.AspNetCore.Components.Authorization.AuthorizeRoute[||]View/>

                <div></div>
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Authorization
                <div></div>

                <AuthorizeRouteView/>

                <div></div>
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent);
    }

    [Fact]
    public async Task SimplifyFullyQualifiedComponent_MultilineWithAttributes()
    {
        await VerifyCodeActionAsync(
            input: """
                <div></div>

                <Microsoft.AspNetCore.Components.Authorization.AuthorizeRoute[||]View
                Resource="test"
                class="goo">
                content
                </Microsoft.AspNetCore.Components.Authorization.AuthorizeRouteView>

                <div></div>
                """,
            expected: """
                @using Microsoft.AspNetCore.Components.Authorization
                <div></div>

                <AuthorizeRouteView
                Resource="test"
                class="goo">
                content
                </AuthorizeRouteView>

                <div></div>
                """,
            codeActionName: LanguageServerConstants.CodeActions.SimplifyFullyQualifiedComponent);
    }
}
