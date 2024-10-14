// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.Completion.Delegation;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation;

public abstract class ResponseRewriterTestBase : LanguageServerTestBase
{
    private protected DelegatedCompletionResponseRewriter Rewriter { get; }

    private protected ResponseRewriterTestBase(
        DelegatedCompletionResponseRewriter rewriter,
        ITestOutputHelper testOutput)
        : base(testOutput)
    {
        Rewriter = rewriter;
    }

    private protected Task<VSInternalCompletionList> GetRewrittenCompletionListAsync(
        int absoluteIndex,
        string documentContent,
        VSInternalCompletionList initialCompletionList,
        DelegatedCompletionResponseRewriter rewriter = null)
    {
        var razorCompletionOptions = new RazorCompletionOptions(
                SnippetsSupported: true,
                AutoInsertAttributeQuotes: true,
                CommitElementsWithSpace: true);

        return GetRewrittenCompletionListAsync(absoluteIndex, documentContent, initialCompletionList, razorCompletionOptions, rewriter);
    }

    private protected async Task<VSInternalCompletionList> GetRewrittenCompletionListAsync(
        int absoluteIndex,
        string documentContent,
        VSInternalCompletionList initialCompletionList,
        RazorCompletionOptions razorCompletionOptions,
        DelegatedCompletionResponseRewriter rewriter = null)
    {
        var completionContext = new VSInternalCompletionContext();
        var codeDocument = CreateCodeDocument(documentContent);
        var documentContext = TestDocumentContext.Create("C:/path/to/file.cshtml", codeDocument);
        var provider = TestDelegatedCompletionListProvider.Create(initialCompletionList, LoggerFactory, rewriter ?? Rewriter);
        var clientCapabilities = new VSInternalClientCapabilities();
        var completionList = await provider.GetCompletionListAsync(
            absoluteIndex,
            completionContext,
            documentContext,
            clientCapabilities,
            razorCompletionOptions,
            correlationId: Guid.Empty,
            cancellationToken: DisposalToken);

        return completionList;
    }
}
