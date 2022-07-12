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
        private static readonly TimeSpan s_batchingTimeSpan = TimeSpan.FromMilliseconds(50);

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
