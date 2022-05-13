// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.VisualStudio.LanguageServer.Protocol;

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

        public Task<Unit> Handle(SemanticTokensRefreshParamsBridge request, CancellationToken cancellationToken)
        {
            // We have to invalidate the tokens cache since it may no longer be up to date.
            _semanticTokensRefreshPublisher.EnqueueWorkspaceSemanticTokensRefresh();

            return Unit.Task;
        }

        public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            const string ServerCapability = "workspace.semanticTokens";

            return new RegistrationExtensionResult(ServerCapability, new SemanticTokenRefreshRegistrationOptions(RefreshSupport: true));
        }

        private record SemanticTokenRefreshRegistrationOptions(bool RefreshSupport);
    }
}
