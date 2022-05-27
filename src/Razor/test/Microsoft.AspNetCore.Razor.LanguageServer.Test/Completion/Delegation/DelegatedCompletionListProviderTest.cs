// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation
{
    public class DelegatedCompletionListProviderTest : LanguageServerTestBase
    {
        public DelegatedCompletionListProviderTest()
        {
            Provider = TestDelegatedCompletionListProvider.Create(LoggerFactory);
            ClientCapabilities = new VSInternalClientCapabilities();
        }

        private TestDelegatedCompletionListProvider Provider { get; }

        private VSInternalClientCapabilities ClientCapabilities { get; }

        [Fact]
        public async Task ResponseRewritersGetExecutedInOrder()
        {
            // Arrange
            var completionContext = new VSInternalCompletionContext();
            var codeDocument = CreateCodeDocument("<");
            var documentContext = TestDocumentContext.From("C:/path/to/file.cshtml", codeDocument);
            var rewriter1 = new TestResponseRewriter(order: 100);
            var rewriter2 = new TestResponseRewriter(order: 20);
            var provider = TestDelegatedCompletionListProvider.Create(LoggerFactory, rewriter1, rewriter2);

            // Act
            var completionList = await provider.GetCompletionListAsync(absoluteIndex: 1, completionContext, documentContext, ClientCapabilities, CancellationToken.None);

            // Assert
            Assert.Collection(completionList.Items,
                item => Assert.Equal("20", item.Label),
                item => Assert.Equal("100", item.Label));
        }

        [Fact]
        public async Task HtmlDelegation_Invoked()
        {
            // Arrange
            var completionContext = new VSInternalCompletionContext() { TriggerKind = CompletionTriggerKind.Invoked };
            var codeDocument = CreateCodeDocument("<");
            var documentContext = TestDocumentContext.From("C:/path/to/file.cshtml", codeDocument, hostDocumentVersion: 1337);

            // Act
            await Provider.GetCompletionListAsync(absoluteIndex: 1, completionContext, documentContext, ClientCapabilities, CancellationToken.None);

            // Assert
            var delegatedParameters = Provider.DelegatedParams;
            Assert.NotNull(delegatedParameters);
            Assert.Equal(RazorLanguageKind.Html, delegatedParameters.ProjectedKind);
            Assert.Equal(new Position(0, 1), delegatedParameters.ProjectedPosition);
            Assert.Equal(CompletionTriggerKind.Invoked, delegatedParameters.Context.TriggerKind);
            Assert.Equal(1337, delegatedParameters.HostDocument.Version);
        }

        [Fact]
        public async Task HtmlDelegation_TriggerCharacter()
        {
            // Arrange
            var completionContext = new VSInternalCompletionContext()
            {
                InvokeKind = VSInternalCompletionInvokeKind.Typing,
                TriggerKind = CompletionTriggerKind.TriggerCharacter,
                TriggerCharacter = "<",
            };
            var codeDocument = CreateCodeDocument("<");
            var documentContext = TestDocumentContext.From("C:/path/to/file.cshtml", codeDocument, hostDocumentVersion: 1337);

            // Act
            await Provider.GetCompletionListAsync(absoluteIndex: 1, completionContext, documentContext, ClientCapabilities, CancellationToken.None);

            // Assert
            var delegatedParameters = Provider.DelegatedParams;
            Assert.NotNull(delegatedParameters);
            Assert.Equal(RazorLanguageKind.Html, delegatedParameters.ProjectedKind);
            Assert.Equal(new Position(0, 1), delegatedParameters.ProjectedPosition);
            Assert.Equal(CompletionTriggerKind.TriggerCharacter, delegatedParameters.Context.TriggerKind);
            Assert.Equal(VSInternalCompletionInvokeKind.Typing, delegatedParameters.Context.InvokeKind);
            Assert.Equal(1337, delegatedParameters.HostDocument.Version);
        }

        [Fact]
        public async Task HtmlDelegation_UnsupportedTriggerCharacter_TranslatesToInvoked()
        {
            // Arrange
            var completionContext = new VSInternalCompletionContext()
            {
                InvokeKind = VSInternalCompletionInvokeKind.Typing,
                TriggerKind = CompletionTriggerKind.TriggerCharacter,
                TriggerCharacter = "|",
            };
            var codeDocument = CreateCodeDocument("|");
            var documentContext = TestDocumentContext.From("C:/path/to/file.cshtml", codeDocument, hostDocumentVersion: 1337);

            // Act
            await Provider.GetCompletionListAsync(absoluteIndex: 1, completionContext, documentContext, ClientCapabilities, CancellationToken.None);

            // Assert
            var delegatedParameters = Provider.DelegatedParams;
            Assert.NotNull(delegatedParameters);
            Assert.Equal(RazorLanguageKind.Html, delegatedParameters.ProjectedKind);
            Assert.Equal(new Position(0, 1), delegatedParameters.ProjectedPosition);
            Assert.Equal(CompletionTriggerKind.Invoked, delegatedParameters.Context.TriggerKind);
            Assert.Equal(VSInternalCompletionInvokeKind.Typing, delegatedParameters.Context.InvokeKind);
            Assert.Equal(1337, delegatedParameters.HostDocument.Version);
        }

        [Fact]
        public async Task CSharpDelegation_Invoked()
        {
            // Arrange
            var completionContext = new VSInternalCompletionContext() { TriggerKind = CompletionTriggerKind.Invoked };
            var codeDocument = CreateCodeDocument("@");
            var documentContext = TestDocumentContext.From("C:/path/to/file.cshtml", codeDocument, hostDocumentVersion: 1337);

            // Act
            await Provider.GetCompletionListAsync(absoluteIndex: 1, completionContext, documentContext, ClientCapabilities, CancellationToken.None);

            // Assert
            var delegatedParameters = Provider.DelegatedParams;
            Assert.NotNull(delegatedParameters);
            Assert.Equal(RazorLanguageKind.CSharp, delegatedParameters.ProjectedKind);

            // Just validating that we're generating code in a way that's different from the top-level document. Don't need to be specific.
            Assert.True(delegatedParameters.ProjectedPosition.Line > 2);
            Assert.Equal(CompletionTriggerKind.Invoked, delegatedParameters.Context.TriggerKind);
            Assert.Equal(1337, delegatedParameters.HostDocument.Version);
        }

        [Fact]
        public async Task CSharpDelegation_TriggerCharacterAt_TranslatesToInvoked()
        {
            // Arrange
            var completionContext = new VSInternalCompletionContext()
            {
                InvokeKind = VSInternalCompletionInvokeKind.Typing,
                TriggerKind = CompletionTriggerKind.TriggerCharacter,
                TriggerCharacter = "@",
            };
            var codeDocument = CreateCodeDocument("@");
            var documentContext = TestDocumentContext.From("C:/path/to/file.cshtml", codeDocument, hostDocumentVersion: 1337);

            // Act
            await Provider.GetCompletionListAsync(absoluteIndex: 1, completionContext, documentContext, ClientCapabilities, CancellationToken.None);

            // Assert
            var delegatedParameters = Provider.DelegatedParams;
            Assert.NotNull(delegatedParameters);
            Assert.Equal(RazorLanguageKind.CSharp, delegatedParameters.ProjectedKind);

            // Just validating that we're generating code in a way that's different from the top-level document. Don't need to be specific.
            Assert.True(delegatedParameters.ProjectedPosition.Line > 2);
            Assert.Equal(CompletionTriggerKind.Invoked, delegatedParameters.Context.TriggerKind);
            Assert.Equal(VSInternalCompletionInvokeKind.Explicit, delegatedParameters.Context.InvokeKind);
            Assert.Equal(1337, delegatedParameters.HostDocument.Version);
        }

        [Fact]
        public async Task CSharpDelegation_TriggerCharacter()
        {
            // Arrange
            var completionContext = new VSInternalCompletionContext()
            {
                InvokeKind = VSInternalCompletionInvokeKind.Typing,
                TriggerKind = CompletionTriggerKind.TriggerCharacter,
                TriggerCharacter = ".",
            };
            var codeDocument = CreateCodeDocument("@{ var abc = DateTime.;}");
            var documentContext = TestDocumentContext.From("C:/path/to/file.cshtml", codeDocument, hostDocumentVersion: 1337);

            // Act
            await Provider.GetCompletionListAsync(absoluteIndex: 22, completionContext, documentContext, ClientCapabilities, CancellationToken.None);

            // Assert
            var delegatedParameters = Provider.DelegatedParams;
            Assert.NotNull(delegatedParameters);
            Assert.Equal(RazorLanguageKind.CSharp, delegatedParameters.ProjectedKind);

            // Just validating that we're generating code in a way that's different from the top-level document. Don't need to be specific.
            Assert.True(delegatedParameters.ProjectedPosition.Line > 2);
            Assert.Equal(CompletionTriggerKind.TriggerCharacter, delegatedParameters.Context.TriggerKind);
            Assert.Equal(VSInternalCompletionInvokeKind.Typing, delegatedParameters.Context.InvokeKind);
            Assert.Equal(1337, delegatedParameters.HostDocument.Version);
        }

        [Fact]
        public async Task CSharpDelegation_UnsupportedTriggerCharacter_TranslatesToInvoked()
        {
            // Arrange
            var completionContext = new VSInternalCompletionContext()
            {
                InvokeKind= VSInternalCompletionInvokeKind.Typing,
                TriggerKind = CompletionTriggerKind.TriggerCharacter,
                TriggerCharacter = "o",
            };
            var codeDocument = CreateCodeDocument("@{ var abc = DateTime.No;}");
            var documentContext = TestDocumentContext.From("C:/path/to/file.cshtml", codeDocument, hostDocumentVersion: 1337);

            // Act
            await Provider.GetCompletionListAsync(absoluteIndex: 24, completionContext, documentContext, ClientCapabilities, CancellationToken.None);

            // Assert
            var delegatedParameters = Provider.DelegatedParams;
            Assert.NotNull(delegatedParameters);
            Assert.Equal(RazorLanguageKind.CSharp, delegatedParameters.ProjectedKind);

            // Just validating that we're generating code in a way that's different from the top-level document. Don't need to be specific.
            Assert.True(delegatedParameters.ProjectedPosition.Line > 2);
            Assert.Equal(CompletionTriggerKind.Invoked, delegatedParameters.Context.TriggerKind);
            Assert.Equal(VSInternalCompletionInvokeKind.Typing, delegatedParameters.Context.InvokeKind);
            Assert.Equal(1337, delegatedParameters.HostDocument.Version);
        }

        private class TestResponseRewriter : DelegatedCompletionResponseRewriter
        {
            private readonly int _order;

            public TestResponseRewriter(int order)
            {
                _order = order;
            }

            public override int Order => _order;

            public override Task<VSInternalCompletionList> RewriteAsync(VSInternalCompletionList completionList, int hostDocumentIndex, DocumentContext hostDocumentContext, DelegatedCompletionParams delegatedParameters, CancellationToken cancellationToken)
            {
                var completionItem = new VSInternalCompletionItem()
                {
                    Label = Order.ToString(),
                };
                completionList.Items = completionList.Items.Concat(new[] { completionItem }).ToArray();

                return Task.FromResult(completionList);
            }
        }

        private class TestDelegatedCompletionListProvider : DelegatedCompletionListProvider
        {
            private readonly DelegatedCompletionRequestResponseFactory _completionFactory;

            private TestDelegatedCompletionListProvider(DelegatedCompletionResponseRewriter[] responseRewriters, ILoggerFactory loggerFactory, DelegatedCompletionRequestResponseFactory completionFactory) :
                base(
                    responseRewriters,
                    new DefaultRazorDocumentMappingService(loggerFactory),
                    new TestOmnisharpLanguageServer(new Dictionary<string, Func<object, object>>()
                    {
                        [LanguageServerConstants.RazorCompletionEndpointName] = completionFactory.OnDelegation,
                    }))
            {
                _completionFactory = completionFactory;
            }

            public static TestDelegatedCompletionListProvider Create(ILoggerFactory loggerFactory, params DelegatedCompletionResponseRewriter[] responseRewriters)
            {
                var requestResponseFactory = new DelegatedCompletionRequestResponseFactory();
                var provider = new TestDelegatedCompletionListProvider(responseRewriters, loggerFactory, requestResponseFactory);
                return provider;
            }

            public DelegatedCompletionParams DelegatedParams => _completionFactory.DelegatedParams;

            private class DelegatedCompletionRequestResponseFactory
            {
                public DelegatedCompletionParams DelegatedParams { get; private set; }

                public object OnDelegation(object parameters)
                {
                    DelegatedParams = (DelegatedCompletionParams)parameters;

                    return new VSInternalCompletionList()
                    {
                        Items = Array.Empty<CompletionItem>(),
                    };
                }
            }
        }
    }
}
