// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodBeAnalysis.Remote.Razor.AutoInsert;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.AutoInsert;
using Microsoft.CodeAnalysis.Razor.Settings;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServices.Razor.LanguageClient.Cohost;
using Microsoft.VisualStudio.Razor.Settings;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostOnAutoInsertEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Theory]
    [InlineData("PageTitle", "$0</PageTitle>", ">")]
    [InlineData("div", "$0</div>", ">")]
    [InlineData("text", "$0</text>", ">")]

    public async Task Component_AutoInsertEndTag(string startTag, string endTag, string triggerCharacter)
    {
        var input = $"""
            This is a Razor document.

            <{startTag}$$

            The end.
            """;

        await VerifyOnAutoInsertAsync(input, endTag, triggerCharacter);
    }

    [Theory]
    [InlineData("div style", "\"\"", "=")]
    public async Task Component_AutoInsertAttributeQuotes(string startTag, string insertedText, string triggerCharacter)
    {
        var input = $"""
            This is a Razor document.

            <{startTag}$$

            The end.
            """;

        await VerifyOnAutoInsertAsync(input, insertedText, triggerCharacter, createDelegatedResponse: true);
    }

    [Theory]
    [InlineData("""
            @code {
                //$$
                void TestMethod() {}
            }
            """,
        "/// <summary>", "/")]
    public async Task Component_AutoInsertCSharp(string input, string insertedText, string triggerCharacter)
    {
        await VerifyOnAutoInsertAsync(input, insertedText, triggerCharacter);
    }

    private async Task VerifyOnAutoInsertAsync(
        string input,
        string insertedText,
        string triggerCharacter,
        bool createDelegatedResponse = false)
    {
        TestFileMarkupParser.GetPosition(input, out input, out var cursorPosition);
        var document = CreateProjectAndRazorDocument(input);
        var sourceText = await document.GetTextAsync(DisposalToken);

        var clientSettingsManager = new ClientSettingsManager([], null, null);
        clientSettingsManager.Update(ClientAdvancedSettings.Default with { FormatOnType = true, AutoClosingTags = true });

        IOnAutoInsertTriggerCharacterProvider[] onAutoInsertTriggerCharacterProviders = [
            new RemoteAutoClosingTagOnAutoInsertProvider(),
            new RemoteCloseTextTagOnAutoInsertProvider()];

        VSInternalDocumentOnAutoInsertResponseItem? response = null;
        if (createDelegatedResponse)
        {
            var start = sourceText.GetPosition(cursorPosition + triggerCharacter.Length);
            var end = start;
            response = new VSInternalDocumentOnAutoInsertResponseItem()
            {
                TextEdit = new TextEdit() { NewText = insertedText, Range = new() { Start = start, End = end } },
                TextEditFormat = InsertTextFormat.Snippet
            };
        }

        var requestInvoker = new TestLSPRequestInvoker([(VSInternalMethods.OnAutoInsertName, response)]);

        var endpoint = new CohostOnAutoInsertEndpoint(
            RemoteServiceInvoker,
            clientSettingsManager,
            onAutoInsertTriggerCharacterProviders,
            TestHtmlDocumentSynchronizer.Instance,
            requestInvoker,
            LoggerFactory);

        var formattingOptions = new FormattingOptions()
        {
            InsertSpaces = true,
            TabSize = 4
        };

        var request = new VSInternalDocumentOnAutoInsertParams()
        {
            TextDocument = new TextDocumentIdentifier()
            {
                Uri = document.CreateUri()
            },
            Position = sourceText.GetPosition(cursorPosition),
            Character = triggerCharacter,
            Options = formattingOptions
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);

        Assert.NotNull(result);

        if (createDelegatedResponse)
        {
            Assert.Equal(response, result);
        }

        Assert.Equal(insertedText, result.TextEdit.NewText);
    }
}
