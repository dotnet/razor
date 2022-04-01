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
    internal class DefaultWorkspaceSemanticTokensRefreshPublisher : WorkspaceSemanticTokensRefreshPublisher
    {
        private const string WorkspaceSemanticTokensRefreshKey = "WorkspaceSemanticTokensRefresh";
        private readonly IClientLanguageServer _languageServer;
        private BatchingWorkQueue? _workQueue;
        private static readonly TimeSpan s_debounceTimeSpan = TimeSpan.FromMilliseconds(250);

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
                var workItem = new SemanticTokensRefreshWorkItem(_languageServer);
                _workQueue?.Enqueue(WorkspaceSemanticTokensRefreshKey, workItem);
            }
        }

        public override void Dispose()
        {
            _workQueue?.Dispose();
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

        internal TestAccessor GetTestAccessor()
            => new(this);

        public override void Initialize(ErrorReporter errorReporter)
        {
            _workQueue = new BatchingWorkQueue(s_debounceTimeSpan, StringComparer.Ordinal, errorReporter: errorReporter);
        }

        internal class TestAccessor
        {
            private readonly DefaultWorkspaceSemanticTokensRefreshPublisher _publisher;

            internal TestAccessor(DefaultWorkspaceSemanticTokensRefreshPublisher publisher)
            {
                _publisher = publisher;
            }

            public void WaitForEmpty()
            {
                var workQueueTestAccessor = _publisher._workQueue!.GetTestAccessor();
                workQueueTestAccessor.NotifyBackgroundWorkCompleted = new ManualResetEventSlim(initialState: false);
                while (workQueueTestAccessor.IsScheduledOrRunning)
                {
                    workQueueTestAccessor.NotifyBackgroundWorkCompleted.Wait(TimeSpan.FromMilliseconds(50));
                }
            }
        }
    }
}
