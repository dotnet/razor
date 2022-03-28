// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class WorkspaceChangedPublisher
    {
        public abstract Task PublishWorkspaceChangedAsync(CancellationToken cancellationToken);
    }

    internal class DefaultWorkspaceChangedPublisher : WorkspaceChangedPublisher
    {
        private readonly ClientNotifierServiceBase _languageServer;

        public DefaultWorkspaceChangedPublisher(ClientNotifierServiceBase languageServer!!)
        {
            _languageServer = languageServer;
        }

        public override async Task PublishWorkspaceChangedAsync(CancellationToken cancellationToken)
        {
            var useWorkspaceRefresh = _languageServer.ClientSettings.Capabilities?.Workspace is not null &&
                _languageServer.ClientSettings.Capabilities.Workspace.SemanticTokens.IsSupported &&
                _languageServer.ClientSettings.Capabilities.Workspace.SemanticTokens.Value.RefreshSupport;

            if (useWorkspaceRefresh)
            {
                var request = await _languageServer.SendRequestAsync(WorkspaceNames.SemanticTokensRefresh);
                await request.ReturningVoid(cancellationToken);
            }
        }
    }
}
