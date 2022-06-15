// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation
{
    public abstract class ResponseRewriterTestBase : LanguageServerTestBase
    {
        private protected abstract DelegatedCompletionResponseRewriter Rewriter { get; }

        protected async Task<VSInternalCompletionList> GetRewrittenCompletionListAsync(int absoluteIndex, string documentContent, VSInternalCompletionList initialCompletionList)
        {
            var completionContext = new VSInternalCompletionContext();
            var codeDocument = CreateCodeDocument(documentContent);
            var documentContext = TestDocumentContext.From("C:/path/to/file.cshtml", codeDocument);
            var provider = TestDelegatedCompletionListProvider.Create(LoggerFactory, initialCompletionList, Rewriter);
            var clientCapabilities = new VSInternalClientCapabilities();
            var completionList = await provider.GetCompletionListAsync(absoluteIndex, completionContext, documentContext, clientCapabilities, CancellationToken.None);
            return completionList;
        }
    }
}
