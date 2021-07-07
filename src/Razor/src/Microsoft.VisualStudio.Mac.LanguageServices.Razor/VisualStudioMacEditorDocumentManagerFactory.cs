// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor.Documents;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Mac.LanguageServices.Razor
{
    [Shared]
    [ExportWorkspaceServiceFactory(typeof(EditorDocumentManager), ServiceLayer.Host)]
    internal class VisualStudioMacEditorDocumentManagerFactory : IWorkspaceServiceFactory
    {
        private readonly SingleThreadedDispatcher _singleThreadedDispatcher;
        private readonly JoinableTaskContext _joinableTaskContext;

        [ImportingConstructor]
        public VisualStudioMacEditorDocumentManagerFactory(
            SingleThreadedDispatcher singleThreadedDispatcher,
            JoinableTaskContext joinableTaskContext)
        {
            if (singleThreadedDispatcher is null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            _singleThreadedDispatcher = singleThreadedDispatcher;
            _joinableTaskContext = joinableTaskContext;
        }

        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
        {
            if (workspaceServices == null)
            {
                throw new ArgumentNullException(nameof(workspaceServices));
            }

            var fileChangeTrackerFactory = workspaceServices.GetRequiredService<FileChangeTrackerFactory>();
            var editorDocumentManager = new VisualStudioMacEditorDocumentManager(_singleThreadedDispatcher, _joinableTaskContext, fileChangeTrackerFactory);
            return editorDocumentManager;
        }
    }
}
