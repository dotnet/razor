﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Settings;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.ProjectSystem;
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
            input: """
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
             expectedItemLabels: ["char", "DateTime", "Exception"]);
    }

    [Fact]
    public async Task CSharpClassMembersAtProvisionalCompletion()
    {
        await VerifyCompletionListAsync(
            input: """
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
             expectedItemLabels: ["DaysInMonth", "IsLeapYear", "Now"]);
    }

    [Fact]
    public async Task CSharpClassesInCodeBlock()
    {
        await VerifyCompletionListAsync(
            input: """
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
             expectedItemLabels: ["char", "DateTime", "Exception"]);
    }

    [Fact]
    public async Task CSharpClassMembersInCodeBlock()
    {
        await VerifyCompletionListAsync(
            input: """
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
             expectedItemLabels: ["DaysInMonth", "IsLeapYear", "Now"]);
    }

    [Fact]
    public async Task CSharpOverrideMethods()
    {
        await VerifyCompletionListAsync(
            input: """
                This is a Razor document.

                <div></div>

                @code{ public override $$ }

                The end.
                """,
             completionContext: new RoslynVSInternalCompletionContext()
             {
                 InvokeKind = RoslynVSInternalCompletionInvokeKind.Explicit,
                 TriggerCharacter = null,
                 TriggerKind = RoslynCompletionTriggerKind.Invoked
             },
             expectedItemLabels: ["Equals(object? obj)", "GetHashCode()", "SetParametersAsync(ParameterView parameters)", "ToString()"]);
    }

    // Tests MarkupTransitionCompletionItemProvider
    [Fact]
    public async Task CSharpMarkupTransitionAndTagHelpersInCodeBlock()
    {
        await VerifyCompletionListAsync(
            input: """
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
             expectedItemLabels: ["text", "EditForm", "InputDate"]);
    }

    [Fact]
    public async Task RazorDirectives()
    {
        var expectedDirectiveLabels = new string[]
            {
                "attribute", "implements", "inherits", "inject", "layout", "namespace", "page",
                "preservewhitespace", "typeparam", "using"
            };
        var expectedDirectiveSnippetLabels = expectedDirectiveLabels.Select(label => $"{label} directive ...");
        var expectedCSharpLabels = new string[] { "char", "DateTime", "Exception" };
        var expectedLabels = expectedDirectiveLabels
            .Concat(expectedDirectiveSnippetLabels)
            .Concat(expectedCSharpLabels)
            .ToArray();

        await VerifyCompletionListAsync(
            input: """
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
             expectedItemLabels: expectedLabels);
    }

    [Fact]
    public async Task ElementNameTagHelpersCompletion()
    {
        await VerifyCompletionListAsync(
            input: """
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
             expectedItemLabels: ["LayoutView", "EditForm", "ValidationMessage"]);
    }

    [Fact]
    public async Task HtmlElementNamesAndTagHelpersCompletion()
    {
        await VerifyCompletionListAsync(
            input: """
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
             delegatedItemLabels: ["div", "h1"]);
    }

    [Fact]
    public async Task HtmlElementDoNotCommitWithSpace()
    {
        await VerifyCompletionListAsync(
            input: """
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
             delegatedItemLabels: ["div", "h1"],
             delegatedItemCommitCharacters: [" ", ">"],
             commitElementsWithSpace: false);
    }

    [Fact]
    public async Task HtmlSnippetsCompletion()
    {
        await VerifyCompletionListAsync(
            input: """
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
             snippetLabels: ["snippet1", "snippet2"]);
    }

    // Tests HTML attributes and DirectiveAttributeTransitionCompletionItemProvider
    [Fact]
    public async Task HtmlAndDirectiveAttributeTransitionNamesCompletion()
    {
        await VerifyCompletionListAsync(
            input: """
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
             delegatedItemLabels: ["style", "dir"]);
    }

    // Tests HTML attributes and DirectiveAttributeCompletionItemProvider
    [Fact]
    public async Task HtmlAndDirectiveAttributeNamesCompletion()
    {
        await VerifyCompletionListAsync(
            input: """
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
             delegatedItemLabels: ["style", "dir"]);
    }

    // Tests HTML attributes and DirectiveAttributeParameterCompletionItemProvider
    [Fact]
    public async Task HtmlAndDirectiveAttributeParameterNamesCompletion()
    {
        await VerifyCompletionListAsync(
            input: """
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
             delegatedItemLabels: ["style", "dir"]);
    }

    [Fact]
    public async Task HtmlAttributeNamesAndTagHelpersCompletion()
    {
        await VerifyCompletionListAsync(
            input: """
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
             delegatedItemLabels: ["style", "dir"]);
    }

    [Fact]
    public async Task TagHelperAttributes_NoAutoInsertQuotes_Completion()
    {
        await VerifyCompletionListAsync(
            input: """
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
             expectedItemLabels: ["FormName", "OnValidSubmit", "@..."],
             autoInsertAttributeQuotes: false);
    }

    private async Task VerifyCompletionListAsync(
        TestCode input,
        RoslynVSInternalCompletionContext completionContext,
        string[] expectedItemLabels,
        string[]? delegatedItemLabels = null,
        string[]? delegatedItemCommitCharacters = null,
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
                    Label = label,
                    CommitCharacters = delegatedItemCommitCharacters,
                    // If test specifies not to commit with space, set kind to element since we remove space
                    // commit from elements only. Otherwise test doesn't care, so set to None
                    Kind = !commitElementsWithSpace ? CompletionItemKind.Element : CompletionItemKind.None,
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

        var endpoint = new CohostDocumentCompletionEndpoint(
            RemoteServiceInvoker,
            clientSettingsManager,
            TestHtmlDocumentSynchronizer.Instance,
            snippetCompletionItemProvider,
            requestInvoker,
            LoggerFactory);

        var request = new RoslynCompletionParams()
        {
            TextDocument = new RoslynTextDocumentIdentifier()
            {
                Uri = document.CreateUri()
            },
            Position = RoslynLspExtensions.GetPosition(sourceText, input.Position),
            Context = completionContext
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);

        Assert.NotNull(result);

        using var _ = HashSetPool<string>.GetPooledObject(out var labelSet);
        labelSet.AddRange(result.Items.Select((item) => item.Label));
        foreach (var expectedItemLabel in expectedItemLabels)
        {
            Assert.Contains(expectedItemLabel, labelSet);
        }

        if (!commitElementsWithSpace)
        {
            Assert.False(result.Items.Any(item => item.CommitCharacters?.First().Contains(" ") ?? false));
        }

        if (!autoInsertAttributeQuotes)
        {
            // Tag helper attributes create InsertText that looks something like
            // "OnValidSubmit=\"$0\"" (for OnValidSubmit attribute). Make sure the value
            // placeholder $0 is not surrounded with quotes if we set AutoInsertAttributeQuotes
            // to false
            Assert.False(result.Items.Any(item => item.InsertText?.Contains("\"$0\"") ?? false));
        }
    }
}
