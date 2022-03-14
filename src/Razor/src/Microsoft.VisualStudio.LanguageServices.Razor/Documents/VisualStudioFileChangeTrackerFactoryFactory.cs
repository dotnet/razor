// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

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
    [ExportWorkspaceServiceFactory(typeof(FileChangeTrackerFactory), ServiceLayer.Host)]
    internal class VisualStudioFileChangeTrackerFactoryFactory : IWorkspaceServiceFactory
    {
        private readonly IVsAsyncFileChangeEx _fileChangeService;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly JoinableTaskContext _joinableTaskContext;

        [ImportingConstructor]
        public VisualStudioFileChangeTrackerFactoryFactory(
            SVsServiceProvider serviceProvider!!,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!,
            JoinableTaskContext joinableTaskContext!!)
        {
            _fileChangeService = serviceProvider.GetService(typeof(SVsFileChangeEx)) as IVsAsyncFileChangeEx;
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _joinableTaskContext = joinableTaskContext;
        }

        public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices!!)
        {
            var errorReporter = workspaceServices.GetRequiredService<ErrorReporter>();
            return new VisualStudioFileChangeTrackerFactory(errorReporter, _fileChangeService, _projectSnapshotManagerDispatcher, _joinableTaskContext);
        }
    }
}
