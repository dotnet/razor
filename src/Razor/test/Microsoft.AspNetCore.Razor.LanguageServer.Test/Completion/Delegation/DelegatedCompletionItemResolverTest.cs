// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation
{
    public class DelegatedCompletionItemResolverTest : LanguageServerTestBase
    {
        public DelegatedCompletionItemResolverTest()
        {
            ClientCapabilities = new VSInternalClientCapabilities();
            var documentContext = TestDocumentContext.From("C:/path/to/file.cshtml");
            CSharpCompletionParams = new DelegatedCompletionParams(documentContext.Identifier, new Position(10, 6), RazorLanguageKind.CSharp, new VSInternalCompletionContext(), ProvisionalTextEdit: null);
            HtmlCompletionParams = new DelegatedCompletionParams(documentContext.Identifier, new Position(0, 0), RazorLanguageKind.Html, new VSInternalCompletionContext(), ProvisionalTextEdit: null);
        }

        private VSInternalClientCapabilities ClientCapabilities { get; }

        private DelegatedCompletionParams CSharpCompletionParams { get; }

        internal DelegatedCompletionParams HtmlCompletionParams { get; }

        [Fact]
        public async Task ResolveAsync_CanNotFindCompletionItem_Noops()
        {
            // Arrange
            var server = TestItemResolverServer.Create();
            var resolver = new DelegatedCompletionItemResolver(server);
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
            var server = TestItemResolverServer.Create();
            var resolver = new DelegatedCompletionItemResolver(server);
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
            var server = TestItemResolverServer.Create();
            var resolver = new DelegatedCompletionItemResolver(server);
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
            var server = TestItemResolverServer.Create();
            var resolver = new DelegatedCompletionItemResolver(server);
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
            // Arrange
            var expectedResolvedItem = new VSInternalCompletionItem();
            var server = TestItemResolverServer.Create(expectedResolvedItem);
            var resolver = new DelegatedCompletionItemResolver(server);
            var item = new VSInternalCompletionItem();
            var containingCompletionList = new VSInternalCompletionList() { Items = new[] { item, } };
            var originalRequestContext = new DelegatedCompletionResolutionContext(CSharpCompletionParams, new object());

            // Act
            var resolvedItem = await resolver.ResolveAsync(item, containingCompletionList, originalRequestContext, ClientCapabilities, CancellationToken.None);

            // Assert
            Assert.Same(CSharpCompletionParams.HostDocument, server.DelegatedParams.HostDocument);
            Assert.Equal(RazorLanguageKind.CSharp, server.DelegatedParams.OriginatingKind);
            Assert.Same(expectedResolvedItem, resolvedItem);
        }

        [Fact]
        public async Task ResolveAsync_Html_Resolves()
        {
            // Arrange
            var expectedResolvedItem = new VSInternalCompletionItem();
            var server = TestItemResolverServer.Create(expectedResolvedItem);
            var resolver = new DelegatedCompletionItemResolver(server);
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

        internal class TestItemResolverServer : TestOmnisharpLanguageServer
        {
            private readonly DelegatedCompletionResolveRequestHandler _requestHandler;

            private TestItemResolverServer(DelegatedCompletionResolveRequestHandler requestHandler) : base(new Dictionary<string, Func<object, object>>()
            {
                [LanguageServerConstants.RazorCompletionResolveEndpointName] = requestHandler.OnDelegation,
            })
            {
                _requestHandler = requestHandler;
            }

            public DelegatedCompletionItemResolveParams DelegatedParams => _requestHandler.DelegatedParams;

            public static TestItemResolverServer Create(VSInternalCompletionItem resolveResponse = null)
            {
                resolveResponse ??= new VSInternalCompletionItem();
                var requestResponseFactory = new DelegatedCompletionResolveRequestHandler(resolveResponse);
                var provider = new TestItemResolverServer(requestResponseFactory);
                return provider;
            }

            private class DelegatedCompletionResolveRequestHandler
            {
                private readonly VSInternalCompletionItem _resolveResponse;

                public DelegatedCompletionResolveRequestHandler(VSInternalCompletionItem resolveResponse)
                {
                    _resolveResponse = resolveResponse;
                }

                public DelegatedCompletionItemResolveParams DelegatedParams { get; private set; }

                public object OnDelegation(object parameters)
                {
                    DelegatedParams = (DelegatedCompletionItemResolveParams)parameters;

                    return _resolveResponse;
                }
            }
        }
    }
}
