// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.Razor.Protocol.DevTools;
using Microsoft.CodeAnalysis.Testing;
using Roslyn.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

public class CohostDevToolsEndpointTest(ITestOutputHelper testOutputHelper) : CohostEndpointTestBase(testOutputHelper)
{
    [Fact]
    public async Task GeneratedDocumentContents_CSharp()
    {
        var input = """
                @{
                    var message = "Hello World";
                }
                <div>@message</div>
                """;

        await VerifyGeneratedDocumentContentsAsync(input, GeneratedDocumentKind.CSharp);
    }

    [Fact]
    public async Task GeneratedDocumentContents_Html()
    {
        var input = """
                @{
                    var message = "Hello World";
                }
                <div>@message</div>
                """;

        await VerifyGeneratedDocumentContentsAsync(input, GeneratedDocumentKind.Html);
    }

    [Fact]
    public async Task GeneratedDocumentContents_Formatting()
    {
        var input = """
                @{
                    var message = "Hello World";
                }
                <div>@message</div>
                """;

        await VerifyGeneratedDocumentContentsAsync(input, GeneratedDocumentKind.Formatting);
    }

    [Fact]
    public async Task GetTagHelpers_ReturnsJson()
    {
        var input = """
                @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
                <div>Test content</div>
                """;

        var razorDocument = await CreateDocumentAsync(input);
        var endpoint = new CohostTagHelpersEndpoint(IncompatibleProjectService, RemoteServiceInvoker);
        
        var request = new TextDocumentIdentifier
        {
            DocumentUri = razorDocument.CreateDocumentUri()
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, razorDocument, DisposalToken);
        
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        
        // Verify it's valid JSON
        var tagHelpers = JsonSerializer.Deserialize<object>(result);
        Assert.NotNull(tagHelpers);
    }

    [Fact]
    public async Task GetSyntaxTree_ReturnsTree()
    {
        var input = """
                @{
                    var message = "Hello World";
                }
                <div>@message</div>
                """;

        var razorDocument = await CreateDocumentAsync(input);
        var endpoint = new CohostSyntaxTreeEndpoint(IncompatibleProjectService, RemoteServiceInvoker);
        
        var request = new SyntaxTreeRequest
        {
            TextDocument = new TextDocumentIdentifier { DocumentUri = razorDocument.CreateDocumentUri() }
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, razorDocument, DisposalToken);
        
        Assert.NotNull(result);
        Assert.NotNull(result.Root);
        Assert.NotEmpty(result.Root.Kind);
        Assert.True(result.Root.SpanLength > 0);
        Assert.NotNull(result.Root.Children);
    }

    private async Task VerifyGeneratedDocumentContentsAsync(string input, GeneratedDocumentKind kind)
    {
        var razorDocument = await CreateDocumentAsync(input);
        var endpoint = new CohostGeneratedDocumentContentsEndpoint(IncompatibleProjectService, RemoteServiceInvoker);
        
        var request = new DocumentContentsRequest
        {
            TextDocument = new TextDocumentIdentifier { DocumentUri = razorDocument.CreateDocumentUri() },
            Kind = kind
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, razorDocument, DisposalToken);
        
        Assert.NotNull(result);
        Assert.NotEmpty(result.Contents);
        Assert.NotEmpty(result.FilePath);
        
        // Verify content based on kind
        switch (kind)
        {
            case GeneratedDocumentKind.CSharp:
                Assert.Contains("message", result.Contents);
                break;
            case GeneratedDocumentKind.Html:
                Assert.Contains("div", result.Contents);
                break;
            case GeneratedDocumentKind.Formatting:
                // Formatting document should contain C# content
                Assert.Contains("message", result.Contents);
                break;
        }
    }
}