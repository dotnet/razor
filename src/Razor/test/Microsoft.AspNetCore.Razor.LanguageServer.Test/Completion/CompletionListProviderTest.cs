// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class CompletionListProviderTest : LanguageServerTestBase
    {
        public CompletionListProviderTest()
        {
            CompletionList1 = new VSInternalCompletionList() { Items = Array.Empty<CompletionItem>() };
            CompletionList2 = new VSInternalCompletionList() { Items = Array.Empty<CompletionItem>() };
            RazorCompletionProvider = new TestRazorCompletionListProvider(CompletionList1, new[] { SharedTriggerCharacter, });
            DelegatedCompletionProvider = new TestDelegatedCompletionListProvider(CompletionList2, new[] { SharedTriggerCharacter, CompletionList2OnlyTriggerCharacter });
        }

        private string SharedTriggerCharacter => "@";

        private string CompletionList2OnlyTriggerCharacter => "<";

        private VSInternalCompletionList CompletionList1 { get; }

        private VSInternalCompletionList CompletionList2 { get; }

        private RazorCompletionListProvider RazorCompletionProvider { get; }

        private DelegatedCompletionListProvider DelegatedCompletionProvider { get; }

        private VSInternalCompletionContext CompletionContext { get; } = new VSInternalCompletionContext();

        private DocumentContext DocumentContext => TestDocumentContext.From("C:/path/to/file.cshtml");

        private VSInternalClientCapabilities ClientCapabilities = new VSInternalClientCapabilities();

        [Fact]
        public async Task MultipleCompletionLists_Merges()
        {
            // Arrange
            var provider = new CompletionListProvider(RazorCompletionProvider, DelegatedCompletionProvider);

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
            var provider = new CompletionListProvider(RazorCompletionProvider, DelegatedCompletionProvider);
            CompletionContext.TriggerKind = CompletionTriggerKind.TriggerCharacter;
            CompletionContext.TriggerCharacter = CompletionList2OnlyTriggerCharacter;

            // Act
            var completionList = await provider.GetCompletionListAsync(absoluteIndex: 0, CompletionContext, DocumentContext, ClientCapabilities, CancellationToken.None);

            // Assert
            Assert.Same(CompletionList2, completionList);
        }

        private class TestDelegatedCompletionListProvider : DelegatedCompletionListProvider
        {
            private readonly VSInternalCompletionList _completionList;

            public TestDelegatedCompletionListProvider(VSInternalCompletionList completionList, IEnumerable<string> triggerCharacters)
                : base(Array.Empty<DelegatedCompletionResponseRewriter>(), null, null, null)
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

        private class TestRazorCompletionListProvider : RazorCompletionListProvider
        {
            private readonly VSInternalCompletionList _completionList;

            public TestRazorCompletionListProvider(VSInternalCompletionList completionList, IEnumerable<string> triggerCharacters)
                : base(null, null, TestLoggerFactory.Instance)
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
