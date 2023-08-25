﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Debugging;

public class RazorProximityExpressionsEndpointTest : LanguageServerTestBase
{
    private readonly IRazorDocumentMappingService _mappingService;

    public RazorProximityExpressionsEndpointTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _mappingService = new RazorDocumentMappingService(
            FilePathService,
            new TestDocumentContextFactory(),
            LoggerFactory);
    }

    [Fact]
    public async Task Handle_UnsupportedDocument_ReturnsNull()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/document.cshtml");
        var codeDocument = CreateCodeDocument(@"
<p>@DateTime.Now</p>");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);

        var diagnosticsEndpoint = new RazorProximityExpressionsEndpoint(_mappingService, LoggerFactory);
        var request = new RazorProximityExpressionsParams()
        {
            Uri = documentPath,
            Position = new Position(1, 0),
        };
        codeDocument.SetUnsupported();
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var response = await diagnosticsEndpoint.HandleRequestAsync(request, requestContext, default);

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public async Task Handle_ReturnsValidExpressions()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/document.cshtml");
        var codeDocument = CreateCodeDocument(@"
<p>@{var abc = 123;}</p>");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);

        var endpoint = new RazorProximityExpressionsEndpoint(_mappingService, LoggerFactory);
        var request = new RazorProximityExpressionsParams()
        {
            Uri = documentPath,
            Position = new Position(1, 8),
        };
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var response = await endpoint.HandleRequestAsync(request, requestContext, default);

        // Assert
        Assert.Contains("abc", response!.Expressions);
        Assert.Contains("this", response!.Expressions);
    }

    [Fact]
    public async Task Handle_StartsInHtml_ReturnsValidExpressions()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/document.cshtml");
        var codeDocument = CreateCodeDocument(@"
<p>@{var abc = 123;}</p>");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);

        var endpoint = new RazorProximityExpressionsEndpoint(_mappingService, LoggerFactory);
        var request = new RazorProximityExpressionsParams()
        {
            Uri = documentPath,
            Position = new Position(1, 0),
        };
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var response = await endpoint.HandleRequestAsync(request, requestContext, default);

        // Assert
        Assert.Contains("abc", response!.Expressions);
        Assert.Contains("this", response!.Expressions);
    }

    [Fact]
    public async Task Handle_StartInHtml_NoCSharpOnLine_ReturnsNull()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/document.cshtml");
        var codeDocument = CreateCodeDocument(@"
<p></p>");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);

        var diagnosticsEndpoint = new RazorProximityExpressionsEndpoint(_mappingService, LoggerFactory);
        var request = new RazorProximityExpressionsParams()
        {
            Uri = documentPath,
            Position = new Position(1, 0),
        };
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var response = await diagnosticsEndpoint.HandleRequestAsync(request, requestContext, default);

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public async Task Handle_InvalidLocation_ReturnsNull()
    {
        // Arrange
        var documentPath = new Uri("C:/path/to/document.cshtml");
        var codeDocument = CreateCodeDocument(@"
<p>@{

    var abc = 123;
}</p>");
        var documentContext = CreateDocumentContext(documentPath, codeDocument);

        var diagnosticsEndpoint = new RazorProximityExpressionsEndpoint(_mappingService, LoggerFactory);
        var request = new RazorProximityExpressionsParams()
        {
            Uri = documentPath,
            Position = new Position(0, 0),
        };
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var response = await diagnosticsEndpoint.HandleRequestAsync(request, requestContext, default);

        // Assert
        Assert.Null(response);
    }
}
