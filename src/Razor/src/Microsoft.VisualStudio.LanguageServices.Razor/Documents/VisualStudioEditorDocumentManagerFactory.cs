// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor.Documents
{
    [Shared]
    [ExportWorkspaceServiceFactory(typeof(EditorDocumentManager), ServiceLayer.Host)]
    internal class VisualStudioEditorDocumentManagerFactory : IWorkspaceServiceFactory
    {
        private readonly SVsServiceProvider _serviceProvider;
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactory;
        private readonly SingleThreadedDispatcher _singleThreadedDispatcher;
        private readonly JoinableTaskContext _joinableTaskContext;

        [ImportingConstructor]
        public VisualStudioEditorDocumentManagerFactory(
            SVsServiceProvider serviceProvider,
            IVsEditorAdaptersFactoryService editorAdaptersFactory,
            SingleThreadedDispatcher singleThreadedDispatcher,
            JoinableTaskContext joinableTaskContext)
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (editorAdaptersFactory is null)
            {
                throw new ArgumentNullException(nameof(editorAdaptersFactory));
            }

            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            _serviceProvider = serviceProvider;
            _editorAdaptersFactory = editorAdaptersFactory;
            _singleThreadedDispatcher = singleThreadedDispatcher;
            _joinableTaskContext = joinableTaskContext;
        }

        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
        {
            if (workspaceServices == null)
            {
                throw new ArgumentNullException(nameof(workspaceServices));
            }

            var runningDocumentTable = (IVsRunningDocumentTable)_serviceProvider.GetService(typeof(SVsRunningDocumentTable));
            var fileChangeTrackerFactory = workspaceServices.GetRequiredService<FileChangeTrackerFactory>();
            return new VisualStudioEditorDocumentManager(
                _singleThreadedDispatcher, _joinableTaskContext, fileChangeTrackerFactory, runningDocumentTable, _editorAdaptersFactory);
        }
    }
}
