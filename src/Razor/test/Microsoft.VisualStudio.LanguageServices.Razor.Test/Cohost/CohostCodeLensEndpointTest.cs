// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostCodeLensEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task Handle_ValidRazorDocument_ReturnsNull()
    {
        // Given that CodeLens support is not yet implemented in Roslyn cohosting,
        // this test verifies that the endpoint infrastructure is in place
        // and returns null as expected when CodeLens is not available.

        var input = """
            @page "/"
            @{
                var message = "Hello World";
            }
            <h1>@message</h1>
            """;

        var document = CreateProjectAndRazorDocument(input);
        var endpoint = new CohostCodeLensEndpoint(RemoteServiceInvoker, IncompatibleProjectService, LoggerFactory);

        var request = new CodeLensParams
        {
            TextDocument = new TextDocumentIdentifier
            {
                DocumentUri = document.CreateUri()
            }
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);

        // For now, we expect null since CodeLens support is not yet implemented in Roslyn cohosting
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_InvalidDocument_ReturnsNull()
    {
        var input = """
            // This is not a valid Razor document
            """;

        var document = CreateProjectAndRazorDocument(input);
        var endpoint = new CohostCodeLensEndpoint(RemoteServiceInvoker, IncompatibleProjectService, LoggerFactory);

        var request = new CodeLensParams
        {
            TextDocument = new TextDocumentIdentifier
            {
                DocumentUri = document.CreateUri()
            }
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, document, DisposalToken);

        Assert.Null(result);
    }
}