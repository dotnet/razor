// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

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
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostOnAutoInsertEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Theory]
    [InlineData("PageTitle")]
    [InlineData("div")]
    [InlineData("text")]
    public async Task Component_AutoInsertEndTag(string startTag)
    {
        var input = $"""
            This is a Razor document.

            <{startTag}>$$

            The end.
            """;
        var output = $"""
            This is a Razor document.

            <{startTag}>$0</{startTag}>

            The end.
            """;

        await VerifyOnAutoInsertAsync(input, output, triggerCharacter: ">");
    }

    [Fact]
    public async Task Component_AutoInsertAttributeQuotes()
    {
        var input = $"""
            This is a Razor document.

            <PageTitle style=$$></PageTitle>

            The end.
            """;
        var output = $"""
            This is a Razor document.

            <PageTitle style="$0"></PageTitle>

            The end.
            """;
        await VerifyOnAutoInsertAsync(input, output, triggerCharacter: "=", delegatedResponseText: "\"$0\"");
    }

    [Fact]
    public async Task Component_AutoInsertCSharp_OnForwardSlash()
    {
        var input = """
        @code {
            ///$$
            void TestMethod() {}
        }
        """;

        var output = """
        @code {
            /// <summary>
            /// $0
            /// </summary>
            void TestMethod() {}
        }
        """;
        await VerifyOnAutoInsertAsync(input, output, triggerCharacter: "/");
    }

    private async Task VerifyOnAutoInsertAsync(
        string input,
        string output,
        string triggerCharacter,
        string? delegatedResponseText = null)
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
        if (delegatedResponseText is not null)
        {
            var start = sourceText.GetPosition(cursorPosition);
            var end = start;
            response = new VSInternalDocumentOnAutoInsertResponseItem()
            {
                TextEdit = new TextEdit() { NewText = delegatedResponseText, Range = new() { Start = start, End = end } },
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

        var change = sourceText.GetTextChange(result.TextEdit);
        sourceText = sourceText.WithChanges(change);

        AssertEx.EqualOrDiff(output, sourceText.ToString());
    }
}
