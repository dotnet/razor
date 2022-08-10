// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class AggregateCompletionListProviderTest : LanguageServerTestBase
    {
        public AggregateCompletionListProviderTest()
        {
            CompletionList1 = new VSInternalCompletionList() { Items = Array.Empty<CompletionItem>() };
            CompletionList2 = new VSInternalCompletionList() { Items = Array.Empty<CompletionItem>() };
            CompletionListProvider1 = new TestCompletionListProvider(CompletionList1, new[] { SharedTriggerCharacter, });
            CompletionListProvider2 = new TestCompletionListProvider(CompletionList2, new[] { SharedTriggerCharacter, CompletionList2OnlyTriggerCharacter });
        }

        private string SharedTriggerCharacter => "@";

        private string CompletionList2OnlyTriggerCharacter => "<";

        private VSInternalCompletionList CompletionList1 { get; }

        private VSInternalCompletionList CompletionList2 { get; }

        private CompletionListProvider CompletionListProvider1 { get; }

        private CompletionListProvider CompletionListProvider2 { get; }

        private VSInternalCompletionContext CompletionContext { get; } = new VSInternalCompletionContext();

        private DocumentContext DocumentContext => TestDocumentContext.From("C:/path/to/file.cshtml");

        private VSInternalClientCapabilities ClientCapabilities = new VSInternalClientCapabilities();

        [Fact]
        public async Task NoCompletionLists_ReturnsNull()
        {
            // Arrange
            var provider = new AggregateCompletionListProvider(Array.Empty<CompletionListProvider>());

            // Act
            var completionList = await provider.GetCompletionListAsync(absoluteIndex: 0, CompletionContext, DocumentContext, ClientCapabilities, CancellationToken.None);

            // Assert
            Assert.Null(completionList);
        }

        [Fact]
        public async Task SingleCompletionList()
        {
            // Arrange
            var provider = new AggregateCompletionListProvider(new[] { CompletionListProvider1 });

            // Act
            var completionList = await provider.GetCompletionListAsync(absoluteIndex: 0, CompletionContext, DocumentContext, ClientCapabilities, CancellationToken.None);

            // Assert
            Assert.Same(CompletionList1, completionList);
        }

        [Fact]
        public async Task MultipleCompletionLists_Merges()
        {
            // Arrange
            var provider = new AggregateCompletionListProvider(new[] { CompletionListProvider1, CompletionListProvider2 });

            // Act
            var completionList = await provider.GetCompletionListAsync(absoluteIndex: 0, CompletionContext, DocumentContext, ClientCapabilities, CancellationToken.None);

            // Assert
            Assert.NotSame(CompletionList1, completionList);
            Assert.NotSame(CompletionList2, completionList);
        }

        [Fact]
        public async Task MultipleCompletionLists_DifferentCommitCharacters_OnlyCallsApplicable()
        {
            // Arrange
            var provider = new AggregateCompletionListProvider(new[] { CompletionListProvider1, CompletionListProvider2 });
            CompletionContext.TriggerKind = CompletionTriggerKind.TriggerCharacter;
            CompletionContext.TriggerCharacter = CompletionList2OnlyTriggerCharacter;

            // Act
            var completionList = await provider.GetCompletionListAsync(absoluteIndex: 0, CompletionContext, DocumentContext, ClientCapabilities, CancellationToken.None);

            // Assert
            Assert.Same(CompletionList2, completionList);
        }

        private class TestCompletionListProvider : CompletionListProvider
        {
            private readonly VSInternalCompletionList _completionList;

            public TestCompletionListProvider(VSInternalCompletionList completionList, IEnumerable<string> triggerCharacters)
            {
                _completionList = completionList;
                TriggerCharacters = triggerCharacters.ToImmutableHashSet();
            }

            public override ImmutableHashSet<string> TriggerCharacters { get; }

            public override Task<VSInternalCompletionList> GetCompletionListAsync(
                int absoluteIndex,
                VSInternalCompletionContext completionContext,
                DocumentContext documentContext,
                VSInternalClientCapabilities clientCapabilities,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(_completionList);
            }
        }
    }
}
