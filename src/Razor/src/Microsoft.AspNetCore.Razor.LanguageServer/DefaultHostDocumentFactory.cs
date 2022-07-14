// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultHostDocumentFactory : HostDocumentFactory, IDisposable
    {
        // Using 10 milliseconds for the delay here because we want document synchronization to be very fast,
        // so that features like completion are not delayed, but at the same time we don't want to do more work
        // than necessary when both C# and HTML documents change at the same time, firing our event handler
        // twice. Through testing 10ms was a good balance towards providing some de-bouncing but having minimal
        // to no impact on results.
        // It's worth noting that the queue implementation means that this delay is not restarted with each new
        // work item, so even in very high speed typing, with changings coming in at sub-10-millisecond speed,
        // the queue will still process documents even if the user doesn't pause at all, but also will not process
        // a document for each keystroke.
        private static readonly TimeSpan s_batchingTimeSpan = TimeSpan.FromMilliseconds(10);

        private readonly BatchingWorkQueue _workQueue;
        private readonly GeneratedDocumentContainerStore _generatedDocumentContainerStore;

        public DefaultHostDocumentFactory(GeneratedDocumentContainerStore generatedDocumentContainerStore, ErrorReporter errorReporter)
        {
            if (generatedDocumentContainerStore is null)
            {
                throw new ArgumentNullException(nameof(generatedDocumentContainerStore));
            }

            if (errorReporter is null)
            {
                throw new ArgumentNullException(nameof(errorReporter));
            }

            _generatedDocumentContainerStore = generatedDocumentContainerStore;
            _workQueue = new BatchingWorkQueue(s_batchingTimeSpan, FilePathComparer.Instance, errorReporter);
        }

        public override HostDocument Create(string filePath, string targetFilePath)
            => Create(filePath, targetFilePath, fileKind: null);

        public override HostDocument Create(string filePath, string targetFilePath, string fileKind)
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (targetFilePath is null)
            {
                throw new ArgumentNullException(nameof(targetFilePath));
            }

            var hostDocument = new HostDocument(filePath, targetFilePath, fileKind);
            hostDocument.GeneratedDocumentContainer.GeneratedCSharpChanged += GeneratedDocumentContainer_Changed;
            hostDocument.GeneratedDocumentContainer.GeneratedHtmlChanged += GeneratedDocumentContainer_Changed;

            return hostDocument;

            void GeneratedDocumentContainer_Changed(object sender, TextChangeEventArgs args)
            {
                var sharedContainer = _generatedDocumentContainerStore.Get(filePath);
                var container = (GeneratedDocumentContainer)sender;
                var latestDocument = (DefaultDocumentSnapshot)container.LatestDocument;

                _workQueue.Enqueue(filePath, new SetOutputWorkItem(sharedContainer, latestDocument));
            }
        }

        public void Dispose()
        {
            _workQueue.Dispose();
        }

        private class SetOutputWorkItem : BatchableWorkItem
        {
            private readonly ReferenceOutputCapturingContainer _sharedContainer;
            private readonly DefaultDocumentSnapshot _latestDocument;

            public SetOutputWorkItem(ReferenceOutputCapturingContainer sharedContainer, DefaultDocumentSnapshot latestDocument)
            {
                _sharedContainer = sharedContainer;
                _latestDocument = latestDocument;
            }

            public override async ValueTask ProcessAsync(CancellationToken cancellationToken)
            {
                await _sharedContainer.SetOutputAndCaptureReferenceAsync(_latestDocument).ConfigureAwait(false);
            }
        }
    }
}
