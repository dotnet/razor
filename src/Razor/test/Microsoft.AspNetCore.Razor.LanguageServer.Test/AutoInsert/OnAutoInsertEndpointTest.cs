﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.AutoInsert;

public class OnAutoInsertEndpointTest : SingleServerDelegatingEndpointTestBase
{
    public OnAutoInsertEndpointTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
    }

    [Fact]
    public async Task Handle_SingleProvider_InvokesProvider()
    {
        // Arrange
        var codeDocument = CreateCodeDocument();
        var razorFilePath = "file://path/test.razor";
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var optionsMonitor = GetOptionsMonitor();
        var insertProvider = new TestOnAutoInsertProvider(">", canResolve: true);
        var endpoint = new OnAutoInsertEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, new[] { insertProvider }, optionsMonitor, LoggerFactory);
        var @params = new VSInternalDocumentOnAutoInsertParams()
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri, },
            Position = new Position(0, 0),
            Character = ">",
            Options = new FormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            },
        };
        var requestContext = await CreateOnAutoInsertRequestContextAsync(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(@params, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(insertProvider.Called);
        Assert.Equal(0, LanguageServer.RequestCount);
    }

    [Fact]
    public async Task Handle_MultipleProviderSameTrigger_UsesSuccessful()
    {
        // Arrange
        var codeDocument = CreateCodeDocument();
        var razorFilePath = "file://path/test.razor";
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var optionsMonitor = GetOptionsMonitor();
        var insertProvider1 = new TestOnAutoInsertProvider(">", canResolve: false)
        {
            ResolvedTextEdit = new TextEdit()
        };
        var insertProvider2 = new TestOnAutoInsertProvider(">", canResolve: true)
        {
            ResolvedTextEdit = new TextEdit()
        };
        var endpoint = new OnAutoInsertEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, new[] { insertProvider1, insertProvider2 }, optionsMonitor, LoggerFactory);
        var @params = new VSInternalDocumentOnAutoInsertParams()
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri, },
            Position = new Position(0, 0),
            Character = ">",
            Options = new FormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            },
        };

        var requestContext = await CreateOnAutoInsertRequestContextAsync(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(@params, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(insertProvider1.Called);
        Assert.True(insertProvider2.Called);
        Assert.Same(insertProvider2.ResolvedTextEdit, result?.TextEdit);
        Assert.Equal(0, LanguageServer.RequestCount);
    }

    [Fact]
    public async Task Handle_MultipleProviderSameTrigger_UsesFirstSuccessful()
    {
        // Arrange
        var codeDocument = CreateCodeDocument();
        var razorFilePath = "file://path/test.razor";
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var optionsMonitor = GetOptionsMonitor();
        var insertProvider1 = new TestOnAutoInsertProvider(">", canResolve: true)
        {
            ResolvedTextEdit = new TextEdit()
        };
        var insertProvider2 = new TestOnAutoInsertProvider(">", canResolve: true)
        {
            ResolvedTextEdit = new TextEdit()
        };
        var endpoint = new OnAutoInsertEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, new[] { insertProvider1, insertProvider2 }, optionsMonitor, LoggerFactory);
        var @params = new VSInternalDocumentOnAutoInsertParams()
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri, },
            Position = new Position(0, 0),
            Character = ">",
            Options = new FormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            },
        };

        var requestContext = await CreateOnAutoInsertRequestContextAsync(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(@params, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(insertProvider1.Called);
        Assert.False(insertProvider2.Called);
        Assert.Same(insertProvider1.ResolvedTextEdit, result?.TextEdit);
    }

    [Fact]
    public async Task Handle_MultipleProviderUnmatchingTrigger_ReturnsNull()
    {
        // Arrange
        var codeDocument = CreateCodeDocument();
        var razorFilePath = "file://path/test.razor";
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var optionsMonitor = GetOptionsMonitor();
        var insertProvider1 = new TestOnAutoInsertProvider(">", canResolve: true);
        var insertProvider2 = new TestOnAutoInsertProvider("<", canResolve: true);
        var endpoint = new OnAutoInsertEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, new[] { insertProvider1, insertProvider2 }, optionsMonitor, LoggerFactory);
        var @params = new VSInternalDocumentOnAutoInsertParams()
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri, },
            Position = new Position(0, 0),
            Character = "!",
            Options = new FormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            },
        };
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(@params, requestContext, DisposalToken);

        // Assert
        Assert.Null(result);
        Assert.False(insertProvider1.Called);
        Assert.False(insertProvider2.Called);
        Assert.Equal(0, LanguageServer.RequestCount);
    }

    [Fact]
    public async Task Handle_DocumentNotFound_ReturnsNull()
    {
        // Arrange
        var codeDocument = CreateCodeDocument();
        var razorFilePath = "file://path/test.razor";
        await CreateLanguageServerAsync(codeDocument, razorFilePath);

        var optionsMonitor = GetOptionsMonitor();
        var insertProvider = new TestOnAutoInsertProvider(">", canResolve: true);
        var endpoint = new OnAutoInsertEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, new[] { insertProvider }, optionsMonitor, LoggerFactory);
        var uri = new Uri("file://path/test.razor");
        var @params = new VSInternalDocumentOnAutoInsertParams()
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri, },
            Position = new Position(0, 0),
            Character = ">",
            Options = new FormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            },
        };
        var requestContext = CreateRazorRequestContext(documentContext: null);

        // Act
        var result = await endpoint.HandleRequestAsync(@params, requestContext, DisposalToken);

        // Assert
        Assert.Null(result);
        Assert.False(insertProvider.Called);
        Assert.Equal(0, LanguageServer.RequestCount);
    }

    [Fact]
    public async Task Handle_UnsupportedCodeDocument_ReturnsNull()
    {
        // Arrange
        var codeDocument = CreateCodeDocument();
        codeDocument.SetUnsupported();
        var razorFilePath = "file://path/test.razor";
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var optionsMonitor = GetOptionsMonitor();
        var insertProvider = new TestOnAutoInsertProvider(">", canResolve: true);
        var endpoint = new OnAutoInsertEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, new[] { insertProvider }, optionsMonitor, LoggerFactory);
        var @params = new VSInternalDocumentOnAutoInsertParams()
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri, },
            Position = new Position(0, 0),
            Character = ">",
            Options = new FormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            },
        };
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(@params, requestContext, DisposalToken);

        // Assert
        Assert.Null(result);
        Assert.False(insertProvider.Called);
        Assert.Equal(0, LanguageServer.RequestCount);
    }

    [Fact]
    public async Task Handle_NoApplicableProvider_CallsProviderAndReturnsNull()
    {
        // Arrange
        var codeDocument = CreateCodeDocument();
        var razorFilePath = "file://path/test.razor";
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var optionsMonitor = GetOptionsMonitor();
        var insertProvider = new TestOnAutoInsertProvider(">", canResolve: false);
        var endpoint = new OnAutoInsertEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, new[] { insertProvider }, optionsMonitor, LoggerFactory);
        var @params = new VSInternalDocumentOnAutoInsertParams()
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri, },
            Position = new Position(0, 0),
            Character = ">",
            Options = new FormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            },
        };
        var requestContext = await CreateOnAutoInsertRequestContextAsync(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(@params, requestContext, DisposalToken);

        // Assert
        Assert.Null(result);
        Assert.True(insertProvider.Called);
        Assert.Equal(0, LanguageServer.RequestCount);
    }

    [Fact]
    public async Task Handle_OnTypeFormattingOff_CSharp_ReturnsNull()
    {
        // Arrange
        var codeDocument = CreateCodeDocument();
        var razorFilePath = "file://path/test.razor";
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var optionsMonitor = GetOptionsMonitor(formatOnType: false);
        var insertProvider = new TestOnAutoInsertProvider(">", canResolve: false);
        var endpoint = new OnAutoInsertEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, new[] { insertProvider }, optionsMonitor, LoggerFactory);
        var @params = new VSInternalDocumentOnAutoInsertParams()
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri, },
            Position = new Position(1, 3),
            Character = "/",
            Options = new FormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            },
        };
        var requestContext = CreateRazorRequestContext(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(@params, requestContext, DisposalToken);

        // Assert
        Assert.Null(result);
        Assert.Equal(0, LanguageServer.RequestCount);
    }

    [Fact]
    public async Task Handle_OnTypeFormattingOff_Html_CallsLanguageServer()
    {
        // Arrange
        var codeDocument = CreateCodeDocument();
        var razorFilePath = "file://path/test.razor";
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var optionsMonitor = GetOptionsMonitor(formatOnType: false);
        var insertProvider = new TestOnAutoInsertProvider("<", canResolve: false);
        var endpoint = new OnAutoInsertEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, new[] { insertProvider }, optionsMonitor, LoggerFactory);
        var @params = new VSInternalDocumentOnAutoInsertParams()
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri, },
            Position = new Position(0, 0),
            Character = "=",
            Options = new FormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            },
        };
        var requestContext = await CreateOnAutoInsertRequestContextAsync(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(@params, requestContext, DisposalToken);

        // Assert
        Assert.Equal(1, LanguageServer.RequestCount);
    }

    [Fact]
    public async Task Handle_AutoInsertAttributeQuotesOff_Html_DoesNotCallLanguageServer()
    {
        // Arrange
        var codeDocument = CreateCodeDocument();
        var razorFilePath = "file://path/test.razor";
        var uri = new Uri(razorFilePath);
        await CreateLanguageServerAsync(codeDocument, razorFilePath);
        var documentContext = CreateDocumentContext(uri, codeDocument);
        var optionsMonitor = GetOptionsMonitor(autoInsertAttributeQuotes: false);
        var insertProvider = new TestOnAutoInsertProvider("<", canResolve: false);
        var endpoint = new OnAutoInsertEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, new[] { insertProvider }, optionsMonitor, LoggerFactory);
        var @params = new VSInternalDocumentOnAutoInsertParams()
        {
            TextDocument = new TextDocumentIdentifier { Uri = uri, },
            Position = new Position(0, 0),
            Character = "=",
            Options = new FormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            },
        };
        var requestContext = await CreateOnAutoInsertRequestContextAsync(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(@params, requestContext, DisposalToken);

        // Assert
        Assert.Equal(0, LanguageServer.RequestCount);
    }

    [Fact]
    public async Task Handle_SingleServer_CSharpDocCommentSnippet()
    {
        // Arrange
        var input = """
                <div>
                </div>

                @functions {
                    ///$$
                    public void M()
                    {
                    }
                }
                """;

        var expected = """
                <div>
                </div>

                @functions {
                    /// <summary>
                    /// $0
                    /// </summary>
                    public void M()
                    {
                    }
                }
                """;

        var character = "/";

        await VerifyCSharpOnAutoInsertAsync(input, expected, character);
    }

    [Fact]
    public async Task Handle_SingleServer_CSharpDocCommentNewLine()
    {
        // Arrange
        var input = """
                <div>
                </div>

                @functions {
                    /// <summary>
                    /// This is some text
                    $$
                    /// </summary>
                    public void M()
                    {
                    }
                }
                """;

        var expected = """
                <div>
                </div>

                @functions {
                    /// <summary>
                    /// This is some text
                    /// $0
                    /// </summary>
                    public void M()
                    {
                    }
                }
                """;

        var character = "\n";

        await VerifyCSharpOnAutoInsertAsync(input, expected, character);
    }

    [Fact]
    public async Task Handle_SingleServer_CSharpBraceMatching()
    {
        // Arrange
        var input = """
                <div>
                </div>

                @functions {
                    public void M()
                    {
                    $$}
                }
                """;

        var expected = """
                <div>
                </div>

                @functions {
                    public void M()
                    {
                        $0
                    }
                }
                """;

        var character = "\n";

        await VerifyCSharpOnAutoInsertAsync(input, expected, character);
    }

    private async Task<RazorRequestContext> CreateOnAutoInsertRequestContextAsync(VersionedDocumentContext? documentContext)
    {
        var lspServices = new Mock<ILspServices>(MockBehavior.Strict);
        lspServices
            .Setup(l => l.GetRequiredService<AdhocWorkspaceFactory>()).Returns(TestAdhocWorkspaceFactory.Instance);
        var formattingService = await TestRazorFormattingService.CreateWithFullSupportAsync();
        lspServices
            .Setup(l => l.GetRequiredService<IRazorFormattingService>())
            .Returns(formattingService);

        var requestContext = CreateRazorRequestContext(documentContext, lspServices: lspServices.Object);

        return requestContext;
    }

    private async Task VerifyCSharpOnAutoInsertAsync(string input, string expected, string character)
    {
        TestFileMarkupParser.GetPosition(input, out input, out var cursorPosition);

        var codeDocument = CreateCodeDocument(input);
        var razorFilePath = "file://path/test.razor";
        await CreateLanguageServerAsync(codeDocument, razorFilePath);

        var optionsMonitor = GetOptionsMonitor();
        var insertProvider = new TestOnAutoInsertProvider("!!!", canResolve: false);
        var providers = new[] { insertProvider };
        var endpoint = new OnAutoInsertEndpoint(LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, providers, optionsMonitor, LoggerFactory);

        codeDocument.GetSourceText().GetLineAndOffset(cursorPosition, out var line, out var offset);
        var @params = new VSInternalDocumentOnAutoInsertParams()
        {
            TextDocument = new TextDocumentIdentifier { Uri = new Uri(razorFilePath), },
            Position = new Position(line, offset),
            Character = character,
            Options = new FormattingOptions
            {
                TabSize = 4,
                InsertSpaces = true
            },
        };
        var documentContext = DocumentContextFactory.TryCreateForOpenDocument(@params.TextDocument);

        var requestContext = await CreateOnAutoInsertRequestContextAsync(documentContext);

        // Act
        var result = await endpoint.HandleRequestAsync(@params, requestContext, DisposalToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(insertProvider.Called);
        Assert.Equal(1, LanguageServer.RequestCount);

        var edits = new[] { result!.TextEdit.ToTextChange(codeDocument.GetSourceText()) };
        var newText = codeDocument.GetSourceText().WithChanges(edits).ToString();
        Assert.Equal(expected, newText);
    }

    private class TestOnAutoInsertProvider : IOnAutoInsertProvider
    {
        private readonly bool _canResolve;

        public TestOnAutoInsertProvider(string triggerCharacter, bool canResolve)
        {
            TriggerCharacter = triggerCharacter;
            _canResolve = canResolve;
        }

        public bool Called { get; private set; }

        public TextEdit? ResolvedTextEdit { get; set; }

        public string TriggerCharacter { get; }

        // Disabling because [NotNullWhen] is available in two Assemblies and causes warnings
        public bool TryResolveInsertion(Position position, FormattingContext context, [NotNullWhen(true)] out TextEdit? edit, out InsertTextFormat format)
        {
            Called = true;
            edit = ResolvedTextEdit!;
            format = default;
            return _canResolve;
        }
    }

    private static RazorCodeDocument CreateCodeDocument()
    {
        return CreateCodeDocument("""

            @{ }
            """);
    }
}
