// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal abstract class RazorSemanticTokensInfoService
    {
        public abstract Task<SemanticTokens?> GetSemanticTokensAsync(TextDocumentIdentifier textDocumentIdentifier, CancellationToken cancellationToken);

        public abstract Task<SemanticTokens?> GetSemanticTokensAsync(TextDocumentIdentifier textDocumentIdentifier, Range? range, CancellationToken cancellationToken);

        public abstract Task<SemanticTokensFullOrDelta?> GetSemanticTokensEditsAsync(TextDocumentIdentifier textDocumentIdentifier, string? previousId, CancellationToken cancellationToken);
    }
}
