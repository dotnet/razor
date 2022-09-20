// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Formatting;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Sdk;
using LanguageServerConstants = Microsoft.AspNetCore.Razor.LanguageServer.Common.LanguageServerConstants;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation
{
    [UseExportProvider]
    public class DelegatedCompletionItemResolverTest : LanguageServerTestBase
    {
        public DelegatedCompletionItemResolverTest()
        {
            ClientCapabilities = new VSInternalClientCapabilities()
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
            var documentContext = TestDocumentContext.From("C:/path/to/file.cshtml");
            CSharpCompletionParams = new DelegatedCompletionParams(documentContext.Identifier, new Position(10, 6), RazorLanguageKind.CSharp, new VSInternalCompletionContext(), ProvisionalTextEdit: null);
            HtmlCompletionParams = new DelegatedCompletionParams(documentContext.Identifier, new Position(0, 0), RazorLanguageKind.Html, new VSInternalCompletionContext(), ProvisionalTextEdit: null);
            DocumentContextFactory = new TestDocumentContextFactory();
            FormattingService = TestRazorFormattingService.Instance;
            MappingService = new DefaultRazorDocumentMappingService(TestLanguageServerFeatureOptions.Instance, new TestDocumentContextFactory(), LoggerFactory);
        }

        private VSInternalClientCapabilities ClientCapabilities { get; }

        private DelegatedCompletionParams CSharpCompletionParams { get; }

        private DelegatedCompletionParams HtmlCompletionParams { get; }

        private DocumentContextFactory DocumentContextFactory { get; }

        private RazorFormattingService FormattingService { get; }

        private RazorDocumentMappingService MappingService { get; }

        [Fact]
        public async Task ResolveAsync_CanNotFindCompletionItem_Noops()
        {
            // Arrange
            var server = TestDelegatedCompletionItemResolverServer.Create();
            var resolver = new DelegatedCompletionItemResolver(DocumentContextFactory, FormattingService, server);
            var item = new VSInternalCompletionItem();
            var notContainingCompletionList = new VSInternalCompletionList();
            var originalRequestContext = new object();

            // Act
            var resolvedItem = await resolver.ResolveAsync(item, notContainingCompletionList, originalRequestContext, ClientCapabilities, CancellationToken.None);

            // Assert
            Assert.Null(resolvedItem);
        }

        [Fact]
        public async Task ResolveAsync_UnknownRequestContext_Noops()
        {
            // Arrange
            var server = TestDelegatedCompletionItemResolverServer.Create();
            var resolver = new DelegatedCompletionItemResolver(DocumentContextFactory, FormattingService, server);
            var item = new VSInternalCompletionItem();
            var containingCompletionList = new VSInternalCompletionList() { Items = new[] { item, } };
            var originalRequestContext = new object();

            // Act
            var resolvedItem = await resolver.ResolveAsync(item, containingCompletionList, originalRequestContext, ClientCapabilities, CancellationToken.None);

            // Assert
            Assert.Null(resolvedItem);
        }

        [Fact]
        public async Task ResolveAsync_UsesItemsData()
        {
            // Arrange
            var server = TestDelegatedCompletionItemResolverServer.Create();
            var resolver = new DelegatedCompletionItemResolver(DocumentContextFactory, FormattingService, server);
            var expectedData = new object();
            var item = new VSInternalCompletionItem()
            {
                Data = expectedData,
            };
            var containingCompletionList = new VSInternalCompletionList() { Items = new[] { item, }, Data = new object() };
            var originalRequestContext = new DelegatedCompletionResolutionContext(CSharpCompletionParams, new object());

            // Act
            await resolver.ResolveAsync(item, containingCompletionList, originalRequestContext, ClientCapabilities, CancellationToken.None);

            // Assert
            Assert.Same(expectedData, server.DelegatedParams.CompletionItem.Data);
        }

        [Fact]
        public async Task ResolveAsync_InheritsOriginalCompletionListData()
        {
            // Arrange
            var server = TestDelegatedCompletionItemResolverServer.Create();
            var resolver = new DelegatedCompletionItemResolver(DocumentContextFactory, FormattingService, server);
            var item = new VSInternalCompletionItem();
            var containingCompletionList = new VSInternalCompletionList() { Items = new[] { item, }, Data = new object() };
            var expectedData = new object();
            var originalRequestContext = new DelegatedCompletionResolutionContext(CSharpCompletionParams, expectedData);

            // Act
            await resolver.ResolveAsync(item, containingCompletionList, originalRequestContext, ClientCapabilities, CancellationToken.None);

            // Assert
            Assert.Same(expectedData, server.DelegatedParams.CompletionItem.Data);
        }

        [Fact]
        public async Task ResolveAsync_CSharp_Resolves()
        {
            // Arrange & Act
            var resolvedItem = await ResolveCompletionItemAsync("@$$", itemToResolve: "typeof", CancellationToken.None).ConfigureAwait(false);

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
            var resolvedItem = await ResolveCompletionItemAsync(input, itemToResolve: "await", CancellationToken.None).ConfigureAwait(false);

            // Assert
            var textChange = resolvedItem.TextEdit.AsTextChange(originalSourceText);
            var actualSourceText = originalSourceText.WithChanges(textChange);
            Assert.True(expectedSourceText.ContentEquals(actualSourceText));
        }

        [Fact]
        public async Task ResolveAsync_Html_Resolves()
        {
            // Arrange
            var expectedResolvedItem = new VSInternalCompletionItem();
            var server = TestDelegatedCompletionItemResolverServer.Create(expectedResolvedItem);
            var resolver = new DelegatedCompletionItemResolver(DocumentContextFactory, FormattingService, server);
            var item = new VSInternalCompletionItem();
            var containingCompletionList = new VSInternalCompletionList() { Items = new[] { item, } };
            var originalRequestContext = new DelegatedCompletionResolutionContext(HtmlCompletionParams, new object());

            // Act
            var resolvedItem = await resolver.ResolveAsync(item, containingCompletionList, originalRequestContext, ClientCapabilities, CancellationToken.None);

            // Assert
            Assert.Same(HtmlCompletionParams.HostDocument, server.DelegatedParams.HostDocument);
            Assert.Equal(RazorLanguageKind.Html, server.DelegatedParams.OriginatingKind);
            Assert.Same(expectedResolvedItem, resolvedItem);
        }

        private async Task<VSInternalCompletionItem> ResolveCompletionItemAsync(string content, string itemToResolve, CancellationToken cancellationToken)
        {
            TestFileMarkupParser.GetPosition(content, out var documentContent, out var cursorPosition);
            var codeDocument = CreateCodeDocument(documentContent);
            await using var csharpServer = await CreateCSharpServerAsync(codeDocument).ConfigureAwait(false);

            var server = TestDelegatedCompletionItemResolverServer.Create(csharpServer);
            var documentContextFactory = new TestDocumentContextFactory("C:/path/to/file.razor", codeDocument);
            var resolver = new DelegatedCompletionItemResolver(documentContextFactory, FormattingService, server);
            var (containingCompletionList, csharpCompletionParams) = await GetCompletionListAndOriginalParamsAsync(cursorPosition, codeDocument, csharpServer).ConfigureAwait(false);

            var originalRequestContext = new DelegatedCompletionResolutionContext(csharpCompletionParams, containingCompletionList.Data);
            var item = (VSInternalCompletionItem)containingCompletionList.Items.FirstOrDefault(item => item.Label == itemToResolve);

            if (item is null)
            {
                throw new XunitException($"Could not locate completion item '{item.Label}' for completion resolve test");
            }

            var resolvedItem = await resolver.ResolveAsync(item, containingCompletionList, originalRequestContext, ClientCapabilities, cancellationToken).ConfigureAwait(false);
            return resolvedItem;
        }

        private async Task<CSharpTestLspServer> CreateCSharpServerAsync(RazorCodeDocument codeDocument)
        {
            var csharpSourceText = codeDocument.GetCSharpSourceText();
            var csharpDocumentUri = new Uri("C:/path/to/file.razor__virtual.g.cs");
            var serverCapabilities = new ServerCapabilities()
            {
                CompletionProvider = new CompletionOptions
                {
                    ResolveProvider = true,
                    TriggerCharacters = new[] { " ", "(", "=", "#", ".", "<", "[", "{", "\"", "/", ":", "~" }
                }
            };
            var csharpServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(csharpSourceText, csharpDocumentUri, serverCapabilities).ConfigureAwait(false);

            await csharpServer.OpenDocumentAsync(csharpDocumentUri, csharpSourceText.ToString()).ConfigureAwait(false);

            return csharpServer;
        }

        private async Task<(VSInternalCompletionList, DelegatedCompletionParams)> GetCompletionListAndOriginalParamsAsync(
            int cursorPosition,
            RazorCodeDocument codeDocument,
            CSharpTestLspServer csharpServer)
        {
            var completionContext = new VSInternalCompletionContext() { TriggerKind = CompletionTriggerKind.Invoked };
            var documentContext = TestDocumentContext.From("C:/path/to/file.razor", codeDocument, hostDocumentVersion: 1337);
            var provider = TestDelegatedCompletionListProvider.Create(csharpServer);

            var completionList = await provider.GetCompletionListAsync(cursorPosition, completionContext, documentContext, ClientCapabilities, CancellationToken.None).ConfigureAwait(false);

            return (completionList, provider.DelegatedParams);
        }

        internal class TestDelegatedCompletionItemResolverServer : TestOmnisharpLanguageServer
        {
            private readonly CompletionResolveRequestResponseFactory _requestHandler;

            private TestDelegatedCompletionItemResolverServer(CompletionResolveRequestResponseFactory requestHandler) : base(new Dictionary<string, Func<object, Task<object>>>()
            {
                [LanguageServerConstants.RazorCompletionResolveEndpointName] = requestHandler.OnCompletionResolveDelegationAsync,
                [LanguageServerConstants.RazorGetFormattingOptionsEndpointName] = requestHandler.OnGetFormattingOptionsAsync,
            })
            {
                _requestHandler = requestHandler;
            }

            public DelegatedCompletionItemResolveParams DelegatedParams => _requestHandler.DelegatedParams;

            public static TestDelegatedCompletionItemResolverServer Create(CSharpTestLspServer csharpServer)
            {
                var requestHandler = new DelegatedCSharpCompletionRequestHandler(csharpServer);
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
                private DelegatedCompletionItemResolveParams _delegatedParams;

                public DelegatedCSharpCompletionRequestHandler(CSharpTestLspServer csharpServer)
                {
                    _csharpServer = csharpServer;
                }

                public override DelegatedCompletionItemResolveParams DelegatedParams => _delegatedParams;

                public override async Task<object> OnCompletionResolveDelegationAsync(object parameters)
                {
                    var resolveParams = (DelegatedCompletionItemResolveParams)parameters;
                    _delegatedParams = resolveParams;

                    var resolvedCompletionItem = await _csharpServer.ExecuteRequestAsync<VSInternalCompletionItem, VSInternalCompletionItem>(
                        Methods.TextDocumentCompletionResolveName,
                        _delegatedParams.CompletionItem,
                        CancellationToken.None).ConfigureAwait(false);

                    return resolvedCompletionItem;
                }
            }

            private abstract class CompletionResolveRequestResponseFactory
            {
                public abstract DelegatedCompletionItemResolveParams DelegatedParams { get; }

                public abstract Task<object> OnCompletionResolveDelegationAsync(object parameters);

                public Task<object> OnGetFormattingOptionsAsync(object parameters)
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
}
