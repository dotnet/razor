// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class SemanticTokensRefreshEndpoint : ISemanticTokensRefreshEndpoint
    {
        private readonly WorkspaceSemanticTokensRefreshPublisher _semanticTokensRefreshPublisher;

        public SemanticTokensRefreshEndpoint(WorkspaceSemanticTokensRefreshPublisher semanticTokensRefreshPublisher)
        {
            if (semanticTokensRefreshPublisher is null)
            {
                throw new ArgumentNullException(nameof(semanticTokensRefreshPublisher));
            }

            _semanticTokensRefreshPublisher = semanticTokensRefreshPublisher;
        }

        public Task<Unit> Handle(SemanticTokensRefreshParams request, CancellationToken cancellationToken)
        {
            // We have to invalidate the tokens cache since it may no longer be up to date.
            _semanticTokensRefreshPublisher.EnqueueWorkspaceSemanticTokensRefresh();

            return Unit.Task;
        }
    }
}
