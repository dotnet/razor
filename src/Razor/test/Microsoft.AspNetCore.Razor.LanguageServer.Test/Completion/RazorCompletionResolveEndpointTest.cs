// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class RazorCompletionResolveEndpointTest : LanguageServerTestBase
    {
        public RazorCompletionResolveEndpointTest()
        {
            CompletionListCache = new CompletionListCache();
            Endpoint = new RazorCompletionResolveEndpoint(new AggregateCompletionItemResolver(new[] { new TestCompletionItemResolver() }, LoggerFactory), CompletionListCache);
            Endpoint.GetRegistration(new VSInternalClientCapabilities());
        }

        private RazorCompletionResolveEndpoint Endpoint { get; }

        private CompletionListCache CompletionListCache { get; }

        [Fact]
        public async Task Handle_UncachedCompletionItem_NoChange()
        {
            // Arrange
            var completionItem = new VSInternalCompletionItem() { Label = "Test" };
            var parameters = ConvertToBridgedItem(completionItem);

            // Act
            var resolvedItem = await Endpoint.Handle(parameters, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(resolvedItem.Documentation);
        }

        [Fact]
        public async Task Handle_EvictedCachedCompletionItem_NoChange()
        {
            // Arrange
            var completionItem = new VSInternalCompletionItem() { Label = "Test" };
            var completionList = new VSInternalCompletionList() { Items = new[] { completionItem } };
            completionList.SetResultId(1337, completionSetting: null);
            var parameters = ConvertToBridgedItem(completionItem);

            // Act
            var resolvedItem = await Endpoint.Handle(parameters, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.Null(resolvedItem.Documentation);
        }

        [Fact]
        public async Task Handle_CachedCompletionItem_Resolves()
        {
            // Arrange
            var completionItem = new VSInternalCompletionItem() { Label = "Test" };
            var completionList = new VSInternalCompletionList() { Items = new[] { completionItem } };
            var resultId = CompletionListCache.Set(completionList, context: null);
            completionList.SetResultId(resultId, completionSetting: null);
            var parameters = ConvertToBridgedItem(completionItem);

            // Act
            var resolvedItem = await Endpoint.Handle(parameters, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(resolvedItem.Documentation);
        }

        [Fact]
        public async Task Handle_MultipleResultIdsIgnoresEvictedResultIds_Resolves()
        {
            // Arrange
            var completionItem = new VSInternalCompletionItem() { Label = "Test" };
            var completionList = new VSInternalCompletionList() { Items = new[] { completionItem } };
            completionList.SetResultId(/* Invalid */ 1337, completionSetting: null);
            var resultId = CompletionListCache.Set(completionList, context: null);
            completionList.SetResultId(resultId, completionSetting: null);
            var parameters = ConvertToBridgedItem(completionItem);

            // Act
            var resolvedItem = await Endpoint.Handle(parameters, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(resolvedItem.Documentation);
        }

        [Fact]
        public async Task Handle_MergedCompletionListFindsProperCompletionList_Resolves()
        {
            // Arrange
            var completionSetting = new VSInternalCompletionSetting() { CompletionList = new VSInternalCompletionListSetting() { Data = true } };
            var completionList1 = new VSInternalCompletionList() { Items = Array.Empty<CompletionItem>() };
            var completion1Context = new object();
            var resultId1 = CompletionListCache.Set(completionList1, completion1Context);
            completionList1.SetResultId(resultId1, completionSetting);

            var completionItem = new VSInternalCompletionItem() { Label = "Test" };
            var completionList2 = new VSInternalCompletionList() { Items = new[] { completionItem } };
            var completion2Context = new object();
            var resultId2 = CompletionListCache.Set(completionList2, completion2Context);
            completionList2.SetResultId(resultId2, completionSetting);
            var mergedCompletionList = CompletionListMerger.Merge(completionList1, completionList2);
            var mergedCompletionItem = mergedCompletionList.Items.Single();
            mergedCompletionItem.Data = mergedCompletionList.Data;
            var parameters = ConvertToBridgedItem(mergedCompletionItem);

            // Act
            var resolvedItem = await Endpoint.Handle(parameters, CancellationToken.None).ConfigureAwait(false);

            // Assert
            Assert.NotNull(resolvedItem.Documentation);
            Assert.Same(completion2Context, resolvedItem.Data);
        }

        private VSCompletionItemBridge ConvertToBridgedItem(CompletionItem completionItem)
        {
            var serialized = Serializer.SerializeObject(completionItem);
            var bridgedItem = Serializer.DeserializeObject<VSCompletionItemBridge>(serialized);
            return bridgedItem;
        }

        private class TestCompletionItemResolver : CompletionItemResolver
        {
            public override Task<VSInternalCompletionItem> ResolveAsync(
                VSInternalCompletionItem item,
                VSInternalCompletionList containingCompletionlist,
                object originalRequestContext,
                VSInternalClientCapabilities clientCapabilities,
                CancellationToken cancellationToken)
            {
                item.Documentation = "I was resolved";
                item.Data = originalRequestContext;
                return Task.FromResult(item);
            }
        }
    }
}
