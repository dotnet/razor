// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class WorkspaceSemanticTokensRefreshPublisher
    {
        public abstract void PublishWorkspaceSemanticTokensRefresh();
    }

    internal class DefaultWorkspaceSemanticTokensRefreshPublisher : WorkspaceSemanticTokensRefreshPublisher
    {
        private readonly IClientLanguageServer _languageServer;

        public DefaultWorkspaceSemanticTokensRefreshPublisher(IClientLanguageServer languageServer!!)
        {
            _languageServer = languageServer;
        }

        public override void PublishWorkspaceSemanticTokensRefresh()
        {
            var useWorkspaceRefresh = _languageServer.ClientSettings.Capabilities?.Workspace is not null &&
                _languageServer.ClientSettings.Capabilities.Workspace.SemanticTokens.IsSupported &&
                _languageServer.ClientSettings.Capabilities.Workspace.SemanticTokens.Value.RefreshSupport;

            if (useWorkspaceRefresh)
            {
                var request = _languageServer.SendRequest(WorkspaceNames.SemanticTokensRefresh);
                _ = request.ReturningVoid(CancellationToken.None);
            }
        }
    }
}
