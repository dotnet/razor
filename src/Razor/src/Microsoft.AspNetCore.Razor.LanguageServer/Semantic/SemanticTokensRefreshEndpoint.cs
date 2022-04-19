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
        private readonly WorkspaceSemanticTokensRefreshPublisher _workspaceSemanticTokensRefreshPublisher;
        private readonly SemanticTokensCacheService _tokensCacheService;

        public SemanticTokensRefreshEndpoint(
            WorkspaceSemanticTokensRefreshPublisher workspaceSemanticTokensRefreshPublisher,
            SemanticTokensCacheService tokensCacheService)
        {
            if (workspaceSemanticTokensRefreshPublisher is null)
            {
                throw new ArgumentNullException(nameof(workspaceSemanticTokensRefreshPublisher));
            }

            if (tokensCacheService is null)
            {
                throw new ArgumentNullException(nameof(tokensCacheService));
            }

            _workspaceSemanticTokensRefreshPublisher = workspaceSemanticTokensRefreshPublisher;
            _tokensCacheService = tokensCacheService;
        }

        public Task<Unit> Handle(SemanticTokensRefreshParams request, CancellationToken cancellationToken)
        {
            _workspaceSemanticTokensRefreshPublisher.EnqueueWorkspaceSemanticTokensRefresh();

            // We have to invalidate the tokens cache since it may no longer be up to date.
            _tokensCacheService.ClearCache();

            return Unit.Task;
        }
    }
}
