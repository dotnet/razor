// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class SemanticTokensRefreshEndpoint : ISemanticTokensRefreshEndpoint
    {
        private readonly WorkspaceSemanticTokensRefreshPublisher _semanticTokensRefreshPublisher;
        private readonly SemanticTokensCacheService _semanticTokensCacheService;

        public SemanticTokensRefreshEndpoint(
            WorkspaceSemanticTokensRefreshPublisher semanticTokensRefreshPublisher,
            SemanticTokensCacheService semanticTokensCacheService)
        {
            if (semanticTokensRefreshPublisher is null)
            {
                throw new ArgumentNullException(nameof(semanticTokensRefreshPublisher));
            }

            if (semanticTokensCacheService is null)
            {
                throw new ArgumentNullException(nameof(semanticTokensCacheService));
            }

            _semanticTokensRefreshPublisher = semanticTokensRefreshPublisher;
            _semanticTokensCacheService = semanticTokensCacheService;
        }

        public Task<Unit> Handle(SemanticTokensRefreshParams request, CancellationToken cancellationToken)
        {
            // We have to invalidate the tokens cache since it may no longer be up to date.
            _semanticTokensCacheService.ClearCache();
            _semanticTokensRefreshPublisher.EnqueueWorkspaceSemanticTokensRefresh();

            return Unit.Task;
        }
    }
}
