// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Xunit;
using Xunit.Abstractions;
using RoslynTextDocumentIdentifier = Roslyn.LanguageServer.Protocol.TextDocumentIdentifier;
using RoslynVSInternalCompletionItem = Roslyn.LanguageServer.Protocol.VSInternalCompletionItem;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostDocumentCompletionResolveEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task ResolveReturnsSelf()
    {
        await VerifyCompletionItemResolveAsync(
            input: """
                This is a Razor document.

                <div st$$></div>

                The end.
                """,
             initialItemLabel: "TestItem1",
             expectedItemLabel: "TestItem1");
    }

    private async Task VerifyCompletionItemResolveAsync(
        TestCode input,
        string initialItemLabel,
        string expectedItemLabel)
    {
        var document = await CreateProjectAndRazorDocumentAsync(input.Text);

        var endpoint = new CohostDocumentCompletionResolveEndpoint();

        var textDocumentIdentifier = new RoslynTextDocumentIdentifier()
        {
            Uri = document.CreateUri()
        };

        var resolutionParams = CohostDocumentCompletionResolveParams.Create(textDocumentIdentifier);

        var request = new RoslynVSInternalCompletionItem()
        {
            Data = JsonSerializer.SerializeToElement(resolutionParams),
            Label = initialItemLabel
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, DisposalToken);

        Assert.Equal(result.Label, expectedItemLabel);
    }
}
