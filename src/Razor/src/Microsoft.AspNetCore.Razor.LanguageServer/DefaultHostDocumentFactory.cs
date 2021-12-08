// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultHostDocumentFactory : HostDocumentFactory
    {
        private readonly GeneratedDocumentContainerStore _generatedDocumentContainerStore;

        public DefaultHostDocumentFactory(GeneratedDocumentContainerStore generatedDocumentContainerStore)
        {
            if (generatedDocumentContainerStore is null)
            {
                throw new ArgumentNullException(nameof(generatedDocumentContainerStore));
            }

            _generatedDocumentContainerStore = generatedDocumentContainerStore;
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

                _ = Task.Factory.StartNew(
                    () => sharedContainer.SetOutputAndCaptureReferenceAsync(latestDocument),
                    CancellationToken.None,
                    TaskCreationOptions.None,
                    TaskScheduler.Default);
            }
        }
    }
}
