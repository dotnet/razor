// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
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
        private const string WorkspaceSemanticTokensRefreshKey = "WorkspaceSemanticTokensRefresh";
        private readonly IClientLanguageServer _languageServer;
        private readonly BatchingWorkQueue _workQueue;
        private static readonly TimeSpan s_debounceTimeSpan = TimeSpan.FromMilliseconds(25);

        public DefaultWorkspaceSemanticTokensRefreshPublisher(IClientLanguageServer languageServer!!)
        {
            _languageServer = languageServer;
            var errorReporterObj = _languageServer.GetService(typeof(ErrorReporter));
            if (errorReporterObj is null || errorReporterObj is not ErrorReporter errorReporter)
            {
                throw new InvalidOperationException();
            }

            _workQueue = new BatchingWorkQueue(s_debounceTimeSpan, StringComparer.Ordinal, errorReporter: errorReporter);
        }

        public override void PublishWorkspaceSemanticTokensRefresh()
        {
            var useWorkspaceRefresh = _languageServer.ClientSettings.Capabilities?.Workspace is not null &&
                _languageServer.ClientSettings.Capabilities.Workspace.SemanticTokens.IsSupported &&
                _languageServer.ClientSettings.Capabilities.Workspace.SemanticTokens.Value.RefreshSupport;

            if (useWorkspaceRefresh)
            {
                var workItem = new SemanticTokensRefreshWorkItem(_languageServer);
                _workQueue.Enqueue(WorkspaceSemanticTokensRefreshKey, workItem);
            }
        }

        private class SemanticTokensRefreshWorkItem : BatchableWorkItem
        {
            private readonly IClientLanguageServer _languageServer;

            public SemanticTokensRefreshWorkItem(IClientLanguageServer languageServer)
            {
                _languageServer = languageServer;
            }

            public override ValueTask ProcessAsync(CancellationToken cancellationToken)
            {
                var request = _languageServer.SendRequest(WorkspaceNames.SemanticTokensRefresh);
                return new ValueTask(request.ReturningVoid(cancellationToken));
            }
        }
    }
}
