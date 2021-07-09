// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using Microsoft.AspNetCore.Razor.OmniSharpPlugin;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common
{
    [Shared]
    [Export(typeof(IRazorDocumentChangeListener))]
    [Export(typeof(IOmniSharpProjectSnapshotManagerChangeTrigger))]
    internal class DocumentChangedSynchronizationService : IRazorDocumentChangeListener, IOmniSharpProjectSnapshotManagerChangeTrigger
    {
        private readonly OmniSharpSingleThreadedDispatcher _singleThreadedDispatcher;
        private OmniSharpProjectSnapshotManagerBase _projectManager;

        [ImportingConstructor]
        public DocumentChangedSynchronizationService(OmniSharpSingleThreadedDispatcher singleThreadedDispatcher)
        {
            if (singleThreadedDispatcher is null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            _singleThreadedDispatcher = singleThreadedDispatcher;
        }

        public void Initialize(OmniSharpProjectSnapshotManagerBase projectManager)
        {
            if (projectManager is null)
            {
                throw new ArgumentNullException(nameof(projectManager));
            }

            _projectManager = projectManager;
        }

        public void RazorDocumentChanged(RazorFileChangeEventArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (args.Kind != RazorFileChangeKind.Changed)
            {
                return;
            }

            var projectFilePath = args.UnevaluatedProjectInstance.ProjectFileLocation.File;
            var documentFilePath = args.FilePath;

            _ = _singleThreadedDispatcher.RunOnDispatcherThreadAsync(
                () => _projectManager.DocumentChanged(projectFilePath, documentFilePath),
                CancellationToken.None).ConfigureAwait(false);
        }
    }
}
