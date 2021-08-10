﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#pragma warning disable CS0618
#nullable enable

using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal abstract class RazorSemanticTokensInfoService
    {
        public abstract Task<SemanticTokens?> GetSemanticTokensAsync(TextDocumentIdentifier textDocumentIdentifier, CancellationToken cancellationToken);

        public abstract Task<SemanticTokens?> GetSemanticTokensAsync(TextDocumentIdentifier textDocumentIdentifier, Range? range, CancellationToken cancellationToken);

        public abstract Task<SemanticTokensFullOrDelta?> GetSemanticTokensEditsAsync(TextDocumentIdentifier textDocumentIdentifier, string? previousId, CancellationToken cancellationToken);
    }
}
