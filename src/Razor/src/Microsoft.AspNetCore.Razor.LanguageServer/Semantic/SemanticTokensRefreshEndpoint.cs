// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic
{
    internal class SemanticTokensRefreshEndpoint : ISemanticTokensRefreshEndpoint
    {
        private readonly WorkspaceSemanticTokensRefreshPublisher _workspaceSemanticTokensRefreshPublisher;

        public SemanticTokensRefreshEndpoint(WorkspaceSemanticTokensRefreshPublisher workspaceSemanticTokensRefreshPublisher)
        {
            _workspaceSemanticTokensRefreshPublisher = workspaceSemanticTokensRefreshPublisher;
        }

        public Task<Unit> Handle(SemanticTokensRefreshParams request, CancellationToken cancellationToken)
        {
            _workspaceSemanticTokensRefreshPublisher.EnqueueWorkspaceSemanticTokensRefresh();
            return Unit.Task;
        }
    }
}
