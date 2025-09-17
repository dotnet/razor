// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
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

        await VerifyGeneratedDocumentContentsAsync(input, GeneratedDocumentKind.CSharp, "message", ".g.cs");
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

        await VerifyGeneratedDocumentContentsAsync(input, GeneratedDocumentKind.Html, "div", ".g.html");
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

        await VerifyGeneratedDocumentContentsAsync(input, GeneratedDocumentKind.Formatting, "message", ".formatting.cs");
    }

    [Fact]
    public async Task GetTagHelpers_ReturnsJson()
    {
        var input = """
                @addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
                <div>Test content</div>
                """;

        var razorDocument = CreateProjectAndRazorDocument(input);
        var endpoint = new CohostTagHelpersEndpoint(IncompatibleProjectService, RemoteServiceInvoker);
        
        var request = new TagHelpersRequest
        {
            TextDocument = new TextDocumentIdentifier { DocumentUri = razorDocument.CreateDocumentUri() },
            Kind = TagHelpersKind.All
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

        var razorDocument = CreateProjectAndRazorDocument(input);
        var endpoint = new CohostSyntaxTreeEndpoint(IncompatibleProjectService, RemoteServiceInvoker);
        
        var request = new SyntaxTreeRequest
        {
            TextDocument = new TextDocumentIdentifier { DocumentUri = razorDocument.CreateDocumentUri() }
        };

        var result = await endpoint.GetTestAccessor().HandleRequestAsync(request, razorDocument, DisposalToken);
        
        Assert.NotNull(result);
        Assert.NotNull(result.Root);
        Assert.NotEmpty(result.Root.Kind);
        Assert.True(result.Root.SpanEnd > result.Root.SpanStart);
        Assert.NotNull(result.Root.Children);
    }

    private async Task VerifyGeneratedDocumentContentsAsync(string input, GeneratedDocumentKind kind, string expectedContentSubstring, string expectedFileExtension)
    {
        var razorDocument = CreateProjectAndRazorDocument(input);
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
        
        // Verify content contains expected substring
        Assert.Contains(expectedContentSubstring, result.Contents);
        
        // Verify file path ends with expected extension
        Assert.EndsWith(expectedFileExtension, result.FilePath);
    }
}