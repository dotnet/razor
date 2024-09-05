// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodBeAnalysis.Remote.Razor.AutoInsert;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.AutoInsert;
using Microsoft.CodeAnalysis.Razor.Settings;
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

    [Theory]
    [InlineData("PageTitle")]
    [InlineData("div")]
    [InlineData("text")]
    public async Task Component_DoNotAutoInsertEndTag_DisabledAutoClosingTags(string startTag)
    {
        var input = $"""
            This is a Razor document.

            <{startTag}>$$

            The end.
            """;
        var output = $"""
            This is a Razor document.

            <{startTag}>

            The end.
            """;

        await VerifyOnAutoInsertAsync(input, output, triggerCharacter: ">", autoClosingTags: false, expectResult: false);
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

    [Fact]
    public async Task Component_DoNotAutoInsertCSharp_OnForwardSlashWithFormatOnTypeDisabled()
    {
        var input = """
        @code {
            ///$$
            void TestMethod() {}
        }
        """;

        var output = """
        @code {
            ///
            void TestMethod() {}
        }
        """;
        await VerifyOnAutoInsertAsync(input, output, triggerCharacter: "/", formatOnType: false, expectResult: false);
    }

    [Fact]
    public async Task Component_AutoInsertCSharp_OnEnter()
    {
        var input = """
        @code {
            void TestMethod() {
        $$}
        }
        """;

        var output = """
        @code {
            void TestMethod()
            {
                $0
            }
        }
        """;
        await VerifyOnAutoInsertAsync(input, output, triggerCharacter: "\n");
    }

    [Fact]
    public async Task Component_AutoInsertCSharp_OnEnter_TwoSpaceIndent()
    {
        var input = """
        @code {
            void TestMethod() {
        $$}
        }
        """;

        var output = """
        @code {
          void TestMethod()
          {
            $0
          }
        }
        """;
        await VerifyOnAutoInsertAsync(input, output, triggerCharacter: "\n", tabSize: 2);
    }

    [Fact]
    public async Task Component_AutoInsertCSharp_OnEnter_UseTabs()
    {
        var input = """
        @code {
            void TestMethod() {
        $$}
        }
        """;

        var tab = '\t';
        var output = $$"""
        @code {
        {{tab}}void TestMethod()
        {{tab}}{
        {{tab}}{{tab}}$0
        {{tab}}}
        }
        """;
        await VerifyOnAutoInsertAsync(input, output, triggerCharacter: "\n", insertSpaces: false);
    }

    private async Task VerifyOnAutoInsertAsync(
        string input,
        string output,
        string triggerCharacter,
        string? delegatedResponseText = null,
        bool insertSpaces = true,
        int tabSize = 4,
        bool formatOnType = true,
        bool autoClosingTags = true,
        bool expectResult = true)
    {
        var testCode = new TestCode(input);
     
        var document = CreateProjectAndRazorDocument(testCode.Text);
        var sourceText = await document.GetTextAsync(DisposalToken);

        var clientSettingsManager = new ClientSettingsManager([], null, null);
        clientSettingsManager.Update(ClientAdvancedSettings.Default with { FormatOnType = formatOnType, AutoClosingTags = autoClosingTags });

        IOnAutoInsertTriggerCharacterProvider[] onAutoInsertTriggerCharacterProviders = [
            new RemoteAutoClosingTagOnAutoInsertProvider(),
            new RemoteCloseTextTagOnAutoInsertProvider()];

        VSInternalDocumentOnAutoInsertResponseItem? response = null;
        if (delegatedResponseText is not null)
        {
            var start = sourceText.GetPosition(testCode.Position);
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
            InsertSpaces = insertSpaces,
            TabSize = tabSize
        };

        var request = new VSInternalDocumentOnAutoInsertParams()
        {
            TextDocument = new TextDocumentIdentifier()
            {
                Uri = document.CreateUri()
            },
            Position = sourceText.GetPosition(testCode.Position),
            Character = triggerCharacter,
            Options = formattingOptions
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);

        if (expectResult)
        {
            Assert.NotNull(result);
        }
        else
        {
            Assert.Null(result);
        }

        if (result is not null)
        {
            var change = sourceText.GetTextChange(result.TextEdit);
            sourceText = sourceText.WithChanges(change);
        }

        AssertEx.EqualOrDiff(output, sourceText.ToString());
    }
}
