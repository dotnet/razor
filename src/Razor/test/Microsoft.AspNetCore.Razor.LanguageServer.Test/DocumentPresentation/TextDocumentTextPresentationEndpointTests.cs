﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.DocumentPresentation;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation;

public class TextDocumentTextPresentationEndpointTests(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    [Fact]
    public async Task Handle_Html_MakesRequest()
    {
        // Arrange
        TestCode code = "<[|d|]iv></div>";

        var codeDocument = CreateCodeDocument(code.Text);

        var uri = new Uri("file://path/test.razor");
        var documentContext = CreateDocumentContext(uri, codeDocument);

        var clientConnection = CreateClientConnection(response: null, verifiable: true);
        var endpoint = CreateEndpoint(clientConnection);

        var parameters = new TextPresentationParams()
        {
            TextDocument = new() { Uri = uri },
            Range = codeDocument.Source.Text.GetRange(code.Span),
            Text = "Hi there"
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        _ = await endpoint.HandleRequestAsync(parameters, requestContext, DisposalToken);

        // Assert
        Mock.Get(clientConnection).Verify();
    }

    [Fact]
    public async Task Handle_CSharp_DoesNotMakeRequest()
    {
        // Arrange
        TestCode code = "@[|c|]ounter";

        var codeDocument = CreateCodeDocument(code.Text);

        var uri = new Uri("file://path/test.razor");
        var documentContext = CreateDocumentContext(uri, codeDocument);

        var clientConnection = StrictMock.Of<IClientConnection>();
        var endpoint = CreateEndpoint(clientConnection);

        var parameters = new TextPresentationParams()
        {
            TextDocument = new() { Uri = uri },
            Range = codeDocument.Source.Text.GetRange(code.Span),
            Text = "Hi there"
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        _ = await endpoint.HandleRequestAsync(parameters, requestContext, DisposalToken);

        // Assert
        Mock.Get(clientConnection)
            .VerifySendRequest<IRazorPresentationParams, WorkspaceEdit?>(CustomMessageNames.RazorTextPresentationEndpoint, Times.Never);
    }

    [Fact]
    public async Task Handle_DocumentNotFound_ReturnsNull()
    {
        // Arrange
        TestCode code = "<[|d|]iv></div>";

        var codeDocument = CreateCodeDocument(code.Text);
        var uri = new Uri("file://path/test.razor");
        var documentContext = CreateDocumentContext(uri, codeDocument);

        var clientConnection = CreateClientConnection(response: null);
        var endpoint = CreateEndpoint(clientConnection);

        var parameters = new TextPresentationParams()
        {
            TextDocument = new() { Uri = uri },
            Range = codeDocument.Source.Text.GetRange(code.Span),
            Text = "Hi there"
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(parameters, requestContext, DisposalToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_UnsupportedCodeDocument_ReturnsNull()
    {
        // Arrange
        TestCode code = "<[|d|]iv></div>";

        var codeDocument = CreateCodeDocument(code.Text);
        codeDocument.SetUnsupported();

        var uri = new Uri("file://path/test.razor");
        var documentContext = CreateDocumentContext(uri, codeDocument);

        var clientConnection = CreateClientConnection(response: new WorkspaceEdit());
        var endpoint = CreateEndpoint(clientConnection);

        var parameters = new TextPresentationParams()
        {
            TextDocument = new() { Uri = uri },
            Range = codeDocument.Source.Text.GetRange(code.Span),
            Text = "Hi there"
        };

        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(parameters, requestContext, DisposalToken);

        // Assert
        Assert.Null(result);
    }

    private TextDocumentTextPresentationEndpoint CreateEndpoint(IClientConnection clientConnection)
        => new(StrictMock.Of<IDocumentMappingService>(), clientConnection, FilePathService, LoggerFactory);

    private static IClientConnection CreateClientConnection(WorkspaceEdit? response, bool verifiable = false)
        => TestMocks.CreateClientConnection(builder =>
        {
            builder.SetupSendRequest<IRazorPresentationParams, WorkspaceEdit?>(CustomMessageNames.RazorTextPresentationEndpoint, response, verifiable);
        });
}
