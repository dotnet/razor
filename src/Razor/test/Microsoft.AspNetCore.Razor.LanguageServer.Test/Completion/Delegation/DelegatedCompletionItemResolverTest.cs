﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Threading;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation;

[UseExportProvider]
public class DelegatedCompletionItemResolverTest : LanguageServerTestBase
{
    private readonly VSInternalClientCapabilities _clientCapabilities;
    private readonly DelegatedCompletionParams _csharpCompletionParams;
    private readonly DelegatedCompletionParams _htmlCompletionParams;
    private readonly DocumentContextFactory _documentContextFactory;
    private readonly AsyncLazy<IRazorFormattingService> _formattingService;

    public DelegatedCompletionItemResolverTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _clientCapabilities = new VSInternalClientCapabilities()
        {
            TextDocument = new()
            {
                Completion = new VSInternalCompletionSetting()
                {
                    CompletionList = new()
                    {
                        Data = true,
                    }
                }
            }
        };

        var documentContext = TestDocumentContext.From("C:/path/to/file.cshtml", hostDocumentVersion: 0);
        _csharpCompletionParams = new DelegatedCompletionParams(documentContext.Identifier, new Position(10, 6), RazorLanguageKind.CSharp, new VSInternalCompletionContext(), ProvisionalTextEdit: null, CorrelationId: Guid.Empty);
        _htmlCompletionParams = new DelegatedCompletionParams(documentContext.Identifier, new Position(0, 0), RazorLanguageKind.Html, new VSInternalCompletionContext(), ProvisionalTextEdit: null, CorrelationId: Guid.Empty);
        _documentContextFactory = new TestDocumentContextFactory();
        _formattingService = new AsyncLazy<IRazorFormattingService>(() => TestRazorFormattingService.CreateWithFullSupportAsync());
    }

    [Fact]
    public async Task ResolveAsync_CanNotFindCompletionItem_Noops()
    {
        // Arrange
        var server = TestDelegatedCompletionItemResolverServer.Create();
        var resolver = new DelegatedCompletionItemResolver(_documentContextFactory, _formattingService.GetValue(), server);
        var item = new VSInternalCompletionItem();
        var notContainingCompletionList = new VSInternalCompletionList();
        var originalRequestContext = new object();

        // Act
        var resolvedItem = await resolver.ResolveAsync(
            item, notContainingCompletionList, originalRequestContext, _clientCapabilities, DisposalToken);

        // Assert
        Assert.Null(resolvedItem);
    }

    [Fact]
    public async Task ResolveAsync_UnknownRequestContext_Noops()
    {
        // Arrange
        var server = TestDelegatedCompletionItemResolverServer.Create();
        var resolver = new DelegatedCompletionItemResolver(_documentContextFactory, _formattingService.GetValue(), server);
        var item = new VSInternalCompletionItem();
        var containingCompletionList = new VSInternalCompletionList() { Items = new[] { item, } };
        var originalRequestContext = new object();

        // Act
        var resolvedItem = await resolver.ResolveAsync(
            item, containingCompletionList, originalRequestContext, _clientCapabilities, DisposalToken);

        // Assert
        Assert.Null(resolvedItem);
    }

    [Fact]
    public async Task ResolveAsync_UsesItemsData()
    {
        // Arrange
        var server = TestDelegatedCompletionItemResolverServer.Create();
        var resolver = new DelegatedCompletionItemResolver(_documentContextFactory, _formattingService.GetValue(), server);
        var expectedData = new object();
        var item = new VSInternalCompletionItem()
        {
            Data = expectedData,
        };
        var containingCompletionList = new VSInternalCompletionList() { Items = new[] { item, }, Data = new object() };
        var originalRequestContext = new DelegatedCompletionResolutionContext(_csharpCompletionParams, new object());

        // Act
        await resolver.ResolveAsync(
            item, containingCompletionList, originalRequestContext, _clientCapabilities, DisposalToken);

        // Assert
        Assert.Same(expectedData, server.DelegatedParams.CompletionItem.Data);
    }

    [Fact]
    public async Task ResolveAsync_InheritsOriginalCompletionListData()
    {
        // Arrange
        var server = TestDelegatedCompletionItemResolverServer.Create();
        var resolver = new DelegatedCompletionItemResolver(_documentContextFactory, _formattingService.GetValue(), server);
        var item = new VSInternalCompletionItem();
        var containingCompletionList = new VSInternalCompletionList() { Items = new[] { item, }, Data = new object() };
        var expectedData = new object();
        var originalRequestContext = new DelegatedCompletionResolutionContext(_csharpCompletionParams, expectedData);

        // Act
        await resolver.ResolveAsync(item, containingCompletionList, originalRequestContext, _clientCapabilities, DisposalToken);

        // Assert
        Assert.Same(expectedData, server.DelegatedParams.CompletionItem.Data);
    }

    [Fact]
    public async Task ResolveAsync_CSharp_Resolves()
    {
        // Arrange & Act
        var resolvedItem = await ResolveCompletionItemAsync("@$$", itemToResolve: "typeof", DisposalToken);

        // Assert
        Assert.NotNull(resolvedItem.Description);
    }

    [Fact]
    public async Task ResolveAsync_CSharp_RemapAndFormatsTextEdit()
    {
        // Arrange
        var input =
            """
                @{
                    Task FooAsync()
                    {
                    awai$$
                    }
                }
                """;
        TestFileMarkupParser.GetPosition(input, out var documentContent, out _);
        var originalSourceText = SourceText.From(documentContent);
        var expectedSourceText = SourceText.From(
            """
                @{
                    async Task FooAsync()
                    {
                        await
                    }
                }
                """);

        // Act
        var resolvedItem = await ResolveCompletionItemAsync(input, itemToResolve: "await", DisposalToken);

        // Assert
        var textChange = resolvedItem.TextEdit.Value.First.ToTextChange(originalSourceText);
        var actualSourceText = originalSourceText.WithChanges(textChange);
        Assert.True(expectedSourceText.ContentEquals(actualSourceText));
    }

    [Fact]
    public async Task ResolveAsync_Html_Resolves()
    {
        // Arrange
        var expectedResolvedItem = new VSInternalCompletionItem();
        var server = TestDelegatedCompletionItemResolverServer.Create(expectedResolvedItem);
        var resolver = new DelegatedCompletionItemResolver(_documentContextFactory, _formattingService.GetValue(), server);
        var item = new VSInternalCompletionItem();
        var containingCompletionList = new VSInternalCompletionList() { Items = new[] { item, } };
        var originalRequestContext = new DelegatedCompletionResolutionContext(_htmlCompletionParams, new object());

        // Act
        var resolvedItem = await resolver.ResolveAsync(
            item, containingCompletionList, originalRequestContext, _clientCapabilities, DisposalToken);

        // Assert
        Assert.Same(_htmlCompletionParams.Identifier, server.DelegatedParams.Identifier);
        Assert.Equal(RazorLanguageKind.Html, server.DelegatedParams.OriginatingKind);
        Assert.Same(expectedResolvedItem, resolvedItem);
    }

    private async Task<VSInternalCompletionItem> ResolveCompletionItemAsync(string content, string itemToResolve, CancellationToken cancellationToken)
    {
        TestFileMarkupParser.GetPosition(content, out var documentContent, out var cursorPosition);
        var codeDocument = CreateCodeDocument(documentContent);
        await using var csharpServer = await CreateCSharpServerAsync(codeDocument);

        var server = TestDelegatedCompletionItemResolverServer.Create(csharpServer, DisposalToken);
        var documentContextFactory = new TestDocumentContextFactory("C:/path/to/file.razor", codeDocument, version: 123);
        var resolver = new DelegatedCompletionItemResolver(documentContextFactory, _formattingService.GetValue(), server);
        var (containingCompletionList, csharpCompletionParams) = await GetCompletionListAndOriginalParamsAsync(
            cursorPosition, codeDocument, csharpServer);

        var originalRequestContext = new DelegatedCompletionResolutionContext(csharpCompletionParams, containingCompletionList.Data);
        var item = (VSInternalCompletionItem)containingCompletionList.Items.FirstOrDefault(item => item.Label == itemToResolve);

        if (item is null)
        {
            throw new XunitException($"Could not locate completion item '{item.Label}' for completion resolve test");
        }

        var resolvedItem = await resolver.ResolveAsync(
            item, containingCompletionList, originalRequestContext, _clientCapabilities, cancellationToken);

        return resolvedItem;
    }

    private async Task<CSharpTestLspServer> CreateCSharpServerAsync(RazorCodeDocument codeDocument)
    {
        var csharpSourceText = codeDocument.GetCSharpSourceText();
        var csharpDocumentUri = new Uri("C:/path/to/file.razor__virtual.g.cs");
        var serverCapabilities = new VSInternalServerCapabilities()
        {
            CompletionProvider = new CompletionOptions
            {
                ResolveProvider = true,
                TriggerCharacters = new[] { " ", "(", "=", "#", ".", "<", "[", "{", "\"", "/", ":", "~" }
            }
        };

        var csharpServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(
            csharpSourceText, csharpDocumentUri, serverCapabilities, DisposalToken);

        await csharpServer.OpenDocumentAsync(csharpDocumentUri, csharpSourceText.ToString());

        return csharpServer;
    }

    private async Task<(VSInternalCompletionList, DelegatedCompletionParams)> GetCompletionListAndOriginalParamsAsync(
        int cursorPosition,
        RazorCodeDocument codeDocument,
        CSharpTestLspServer csharpServer)
    {
        var completionContext = new VSInternalCompletionContext() { TriggerKind = CompletionTriggerKind.Invoked };
        var documentContext = TestDocumentContext.From("C:/path/to/file.razor", codeDocument, hostDocumentVersion: 1337);
        var provider = TestDelegatedCompletionListProvider.Create(csharpServer, LoggerFactory, DisposalToken);

        var completionList = await provider.GetCompletionListAsync(
            cursorPosition, completionContext, documentContext, _clientCapabilities, correlationId: Guid.Empty, cancellationToken: DisposalToken);

        return (completionList, provider.DelegatedParams);
    }

    internal class TestDelegatedCompletionItemResolverServer : TestLanguageServer
    {
        private readonly CompletionResolveRequestResponseFactory _requestHandler;

        private TestDelegatedCompletionItemResolverServer(CompletionResolveRequestResponseFactory requestHandler)
            : base(new Dictionary<string, Func<object, Task<object>>>()
        {
            [LanguageServerConstants.RazorCompletionResolveEndpointName] = requestHandler.OnCompletionResolveDelegationAsync,
            [LanguageServerConstants.RazorGetFormattingOptionsEndpointName] = requestHandler.OnGetFormattingOptionsAsync,
        })
        {
            _requestHandler = requestHandler;
        }

        public DelegatedCompletionItemResolveParams DelegatedParams => _requestHandler.DelegatedParams;

        public static TestDelegatedCompletionItemResolverServer Create(
            CSharpTestLspServer csharpServer,
            CancellationToken cancellationToken)
        {
            var requestHandler = new DelegatedCSharpCompletionRequestHandler(csharpServer, cancellationToken);
            var provider = new TestDelegatedCompletionItemResolverServer(requestHandler);
            return provider;
        }

        public static TestDelegatedCompletionItemResolverServer Create(VSInternalCompletionItem resolveResponse = null)
        {
            resolveResponse ??= new VSInternalCompletionItem();
            var requestResponseFactory = new StaticCompletionResolveRequestHandler(resolveResponse);
            var provider = new TestDelegatedCompletionItemResolverServer(requestResponseFactory);
            return provider;
        }

        private class StaticCompletionResolveRequestHandler : CompletionResolveRequestResponseFactory
        {
            private readonly VSInternalCompletionItem _resolveResponse;
            private DelegatedCompletionItemResolveParams _delegatedParams;

            public StaticCompletionResolveRequestHandler(VSInternalCompletionItem resolveResponse)
            {
                _resolveResponse = resolveResponse;
            }

            public override DelegatedCompletionItemResolveParams DelegatedParams => _delegatedParams;

            public override Task<object> OnCompletionResolveDelegationAsync(object parameters)
            {
                var resolveParams = (DelegatedCompletionItemResolveParams)parameters;
                _delegatedParams = resolveParams;

                return Task.FromResult<object>(_resolveResponse);
            }
        }

        private class DelegatedCSharpCompletionRequestHandler : CompletionResolveRequestResponseFactory
        {
            private readonly CSharpTestLspServer _csharpServer;
            private readonly CancellationToken _cancellationToken;
            private DelegatedCompletionItemResolveParams _delegatedParams;

            public DelegatedCSharpCompletionRequestHandler(
                CSharpTestLspServer csharpServer,
                CancellationToken cancellationToken)
            {
                _csharpServer = csharpServer;
                _cancellationToken = cancellationToken;
            }

            public override DelegatedCompletionItemResolveParams DelegatedParams => _delegatedParams;

            public override async Task<object> OnCompletionResolveDelegationAsync(object parameters)
            {
                var resolveParams = (DelegatedCompletionItemResolveParams)parameters;
                _delegatedParams = resolveParams;

                var resolvedCompletionItem = await _csharpServer.ExecuteRequestAsync<VSInternalCompletionItem, VSInternalCompletionItem>(
                    Methods.TextDocumentCompletionResolveName,
                    _delegatedParams.CompletionItem,
                    _cancellationToken);

                return resolvedCompletionItem;
            }
        }

        private abstract class CompletionResolveRequestResponseFactory
        {
            public abstract DelegatedCompletionItemResolveParams DelegatedParams { get; }

            public abstract Task<object> OnCompletionResolveDelegationAsync(object parameters);

#pragma warning disable IDE0060 // Remove unused parameter
            public Task<object> OnGetFormattingOptionsAsync(object parameters)
#pragma warning restore IDE0060 // Remove unused parameter
            {
                var formattingOptions = new FormattingOptions()
                {
                    InsertSpaces = true,
                    TabSize = 4,
                };

                return Task.FromResult<object>(formattingOptions);
            }
        }
    }
}
