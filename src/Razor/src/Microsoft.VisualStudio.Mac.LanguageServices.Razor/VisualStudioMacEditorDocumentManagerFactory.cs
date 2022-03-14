// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

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
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly JoinableTaskContext _joinableTaskContext;

        [ImportingConstructor]
        public VisualStudioMacEditorDocumentManagerFactory(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!,
            JoinableTaskContext joinableTaskContext)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _joinableTaskContext = joinableTaskContext;
        }

        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices!!)
        {
            var fileChangeTrackerFactory = workspaceServices.GetRequiredService<FileChangeTrackerFactory>();
            var editorDocumentManager = new VisualStudioMacEditorDocumentManager(_projectSnapshotManagerDispatcher, _joinableTaskContext, fileChangeTrackerFactory);
            return editorDocumentManager;
        }
    }
}
