// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Settings;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Razor.Settings;
using Microsoft.VisualStudio.Razor.Snippets;
using Xunit;
using Xunit.Abstractions;
using RoslynCompletionParams = Roslyn.LanguageServer.Protocol.CompletionParams;
using RoslynCompletionTriggerKind = Roslyn.LanguageServer.Protocol.CompletionTriggerKind;
using RoslynLspExtensions = Roslyn.LanguageServer.Protocol.RoslynLspExtensions;
using RoslynTextDocumentIdentifier = Roslyn.LanguageServer.Protocol.TextDocumentIdentifier;
using RoslynVSInternalCompletionContext = Roslyn.LanguageServer.Protocol.VSInternalCompletionContext;
using RoslynVSInternalCompletionInvokeKind = Roslyn.LanguageServer.Protocol.VSInternalCompletionInvokeKind;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostDocumentCompletionEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task CSharpClassesAtTransition()
    {
        await VerifyCompletionListAsync(
            input: $"""
                This is a Razor document.

                <div>@$$</div>

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Typing,
                 TriggerCharacter = "@",
                 TriggerKind = RoslynCompletionTriggerKind.TriggerCharacter
             },
             expectedItemLabels: ["char", "DateTime", "Exception"],
             expectedItemCount: 996);
    }

    [Fact]
    public async Task CSharpClassMembersAtProvisionalCompletion()
    {
        await VerifyCompletionListAsync(
            input: $"""
                This is a Razor document.

                <div>@DateTime.$$</div>

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Typing,
                 TriggerCharacter = ".",
                 TriggerKind = RoslynCompletionTriggerKind.TriggerCharacter
             },
             expectedItemLabels: ["DaysInMonth", "IsLeapYear", "Now"],
             expectedItemCount: 20);
    }

    [Fact]
    public async Task CSharpClassesInCodeBlock()
    {
        await VerifyCompletionListAsync(
            input: $$"""
                This is a Razor document.

                <div></div>

                @code{ $$ }

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Explicit,
                 TriggerCharacter = null,
                 TriggerKind = RoslynCompletionTriggerKind.Invoked
             },
             expectedItemLabels: ["char", "DateTime", "Exception"],
             expectedItemCount: 1000);
    }

    [Fact]
    public async Task CSharpClassMembersInCodeBlock()
    {
        await VerifyCompletionListAsync(
            input: $$"""
                This is a Razor document.

                <div></div>

                @code{
                    void foo()
                    {
                        DateTime.$$
                    }
                }

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Typing,
                 TriggerCharacter = ".",
                 TriggerKind = RoslynCompletionTriggerKind.TriggerCharacter
             },
             expectedItemLabels: ["DaysInMonth", "IsLeapYear", "Now"],
             expectedItemCount: 20);
    }

    // Tests MarkupTransitionCompletionItemProvider
    [Fact]
    public async Task CSharpMarkupTransitionAndTagHelpersInCodeBlock()
    {
        await VerifyCompletionListAsync(
            input: $$"""
                This is a Razor document.

                <div></div>

                @code{
                    void foo()
                    {
                        <$$
                    }
                }

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Typing,
                 TriggerCharacter = "<",
                 TriggerKind = RoslynCompletionTriggerKind.TriggerCharacter
             },
             expectedItemLabels: ["text", "EditForm", "InputDate"],
             expectedItemCount: 34);
    }

    [Fact]
    public async Task RazorDirectives()
    {
        await VerifyCompletionListAsync(
            input: $$"""
                @$$
                This is a Razor document.

                <div></div>

                @code{
                    void foo()
                    {
                        
                    }
                }

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Typing,
                 TriggerCharacter = "@",
                 TriggerKind = RoslynCompletionTriggerKind.TriggerCharacter
             },
             expectedItemLabels: ["using", "using directive ...", "page", "page directive ..."],
             expectedItemCount: 538);
    }

    [Fact]
    public async Task ElementNameTagHelpersCompletion()
    {
        await VerifyCompletionListAsync(
            input: $"""
                This is a Razor document.

                <$$

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Typing,
                 TriggerCharacter = "<",
                 TriggerKind = RoslynCompletionTriggerKind.TriggerCharacter
             },
             expectedItemLabels: ["LayoutView", "EditForm", "ValidationMessage"],
             expectedItemCount: 33);
    }

    [Fact]
    public async Task HtmlElementNamesAndTagHelpersCompletion()
    {
        await VerifyCompletionListAsync(
            input: $"""
                This is a Razor document.

                <$$

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Typing,
                 TriggerCharacter = "<",
                 TriggerKind = RoslynCompletionTriggerKind.TriggerCharacter
             },
             expectedItemLabels: ["div", "h1", "LayoutView", "EditForm", "ValidationMessage"],
             expectedItemCount: 35,
             delegatedItemLabels: ["div", "h1"]);
    }

    [Fact]
    public async Task HtmlSnippetsCompletion()
    {
        await VerifyCompletionListAsync(
            input: $"""
                This is a Razor document.

                $$

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Explicit,
                 TriggerCharacter = null,
                 TriggerKind = RoslynCompletionTriggerKind.Invoked
             },
             expectedItemLabels: ["snippet1", "snippet2"],
             expectedItemCount: 2,
             snippetLabels: ["snippet1", "snippet2"]);
    }

    // Tests HTML attributes and DirectiveAttributeTransitionCompletionItemProvider
    [Fact]
    public async Task HtmlAndDirectiveAttributeTransitionNamesCompletion()
    {
        await VerifyCompletionListAsync(
            input: $"""
                This is a Razor document.

                <div $$></div>

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Typing,
                 TriggerCharacter = " ",
                 TriggerKind = RoslynCompletionTriggerKind.TriggerCharacter
             },
             expectedItemLabels: ["style", "dir", "@..."],
             expectedItemCount: 3,
             delegatedItemLabels: ["style", "dir"]);
    }

    // Tests HTML attributes and DirectiveAttributeCompletionItemProvider
    [Fact]
    public async Task HtmlAndDirectiveAttributeNamesCompletion()
    {
        await VerifyCompletionListAsync(
            input: $"""
                This is a Razor document.

                <div @$$></div>

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Typing,
                 TriggerCharacter = "@",
                 TriggerKind = RoslynCompletionTriggerKind.TriggerCharacter
             },
             expectedItemLabels: ["style", "dir", "@rendermode", "@bind-..."],
             expectedItemCount: 104,
             delegatedItemLabels: ["style", "dir"]);
    }

    // Tests HTML attributes and DirectiveAttributeParameterCompletionItemProvider
    [Fact]
    public async Task HtmlAndDirectiveAttributeParameterNamesCompletion()
    {
        await VerifyCompletionListAsync(
            input: $"""
                This is a Razor document.

                <input @bind:f$$></div>

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Typing,
                 TriggerCharacter = "f",
                 TriggerKind = RoslynCompletionTriggerKind.TriggerCharacter
             },
             expectedItemLabels: ["style", "dir", "culture", "event", "format", "get", "set", "after"],
             expectedItemCount: 8,
             delegatedItemLabels: ["style", "dir"]);
    }

    [Fact]
    public async Task HtmlAttributeNamesAndTagHelpersCompletion()
    {
        await VerifyCompletionListAsync(
            input: $"""
                This is a Razor document.

                <EditForm $$></EditForm>

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Typing,
                 TriggerCharacter = " ",
                 TriggerKind = RoslynCompletionTriggerKind.TriggerCharacter
             },
             expectedItemLabels: ["style", "dir", "FormName", "OnValidSubmit", "@..."],
             expectedItemCount: 13,
             delegatedItemLabels: ["style", "dir"]);
    }

    private async Task VerifyCompletionListAsync(
        TestCode input,
        RoslynVSInternalCompletionContext completionContext,
        string[] expectedItemLabels,
        int expectedItemCount,
        string[]? delegatedItemLabels = null,
        string[]? snippetLabels = null,
        bool autoInsertAttributeQuotes = true,
        bool commitElementsWithSpace = true)
    {
        var document = await CreateProjectAndRazorDocumentAsync(input.Text);
        var sourceText = await document.GetTextAsync(DisposalToken);

        var clientSettingsManager = new ClientSettingsManager([], null, null);
        clientSettingsManager.Update(ClientAdvancedSettings.Default with { AutoInsertAttributeQuotes = autoInsertAttributeQuotes, CommitElementsWithSpace = commitElementsWithSpace });

        VSInternalCompletionList? response = null;
        if (delegatedItemLabels is not null)
        {
            response = new VSInternalCompletionList()
            {
                Items = delegatedItemLabels.Select((label) => new VSInternalCompletionItem()
                {
                    Label = label
                }).ToArray(),
                IsIncomplete = true
            };
        }

        var requestInvoker = new TestLSPRequestInvoker([(Methods.TextDocumentCompletionName, response)]);

        var snippetCompletionItemProvider = new SnippetCompletionItemProvider(new SnippetCache());
        if (snippetLabels is not null)
        {
            var snippetInfos = snippetLabels.Select(label => new SnippetInfo(label, label, label, string.Empty, SnippetLanguage.Html)).ToImmutableArray();
            snippetCompletionItemProvider.SnippetCache.Update(SnippetLanguage.Html, snippetInfos);
        }

        var completionSetting = new CompletionSetting
        {
            CompletionItem = new CompletionItemSetting(),
            CompletionItemKind = new CompletionItemKindSetting()
            {
                ValueSet = (CompletionItemKind[])Enum.GetValues(typeof(CompletionItemKind)),
            },
            CompletionListSetting = new CompletionListSetting()
            {
                ItemDefaults = ["commitCharacters", "editRange", "insertTextFormat"]
            },
            ContextSupport = false,
            InsertTextMode = InsertTextMode.AsIs,
        };
        var clientCapabilities = new VSInternalClientCapabilities
        {
            TextDocument = new TextDocumentClientCapabilities
            {
                Completion = completionSetting
            }
        };
        var endpoint = new CohostDocumentCompletionEndpoint(
            RemoteServiceInvoker,
            clientSettingsManager,
            TestHtmlDocumentSynchronizer.Instance,
            snippetCompletionItemProvider,
            requestInvoker,
            LoggerFactory);
        endpoint.GetTestAccessor().SetClientCapabilities(clientCapabilities);

        var request = new RoslynCompletionParams()
        {
            TextDocument = new RoslynTextDocumentIdentifier()
            {
                Uri = document.CreateUri()
            },
            Position = RoslynLspExtensions.GetPosition(sourceText, input.Position),
            Context = completionContext
        };

        // Roslyn doesn't always return all items right away, so using retry logic
        VSInternalCompletionList? result = null;
        var resultCount = 0;
        const int maxResultCount = 100;
        do
        {
            if (resultCount > 0)
            {
                await Task.Delay(100);
            }

            result = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);
        }
        while (result is not null
               && result.IsIncomplete
               && result.Items.Length < expectedItemCount
               && resultCount++ < maxResultCount);

        Assert.NotNull(result);
        if (result is not null)
        {
            Assert.Equal(expectedItemCount, result.Items.Length);
            var labelSet = new HashSet<string>(result.Items.Select((item) => item.Label));
            foreach (var expectedItemLabel in expectedItemLabels)
            {
                Assert.Contains(expectedItemLabel, labelSet);
            }
        }
    }
}
