// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.LiveShare.Razor.Host
{
    internal class DefaultProjectHierarchyProxy : IProjectHierarchyProxy, ICollaborationService
    {
        private readonly CollaborationSession _session;

        private readonly JoinableTaskFactory _joinableTaskFactory;
        private IVsUIShellOpenDocument _openDocumentShell;

        public DefaultProjectHierarchyProxy(
            CollaborationSession session!!,
            JoinableTaskFactory joinableTaskFactory!!)
        {
            _session = session;
            _joinableTaskFactory = joinableTaskFactory;
        }

        public async Task<Uri> GetProjectPathAsync(Uri documentFilePath!!, CancellationToken cancellationToken)
        {
            await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            if (_openDocumentShell is null)
            {
                _openDocumentShell = ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
            }

            var hostDocumentFilePath = _session.ConvertSharedUriToLocalPath(documentFilePath);
            var hr = _openDocumentShell.IsDocumentInAProject(hostDocumentFilePath, out var hierarchy, out _, out _, out _);
            if (ErrorHandler.Succeeded(hr) && hierarchy != null)
            {
                ErrorHandler.ThrowOnFailure(((IVsProject)hierarchy).GetMkDocument((uint)VSConstants.VSITEMID.Root, out var path), VSConstants.E_NOTIMPL);

                return _session.ConvertLocalPathToSharedUri(path);
            }

            return null;
        }
    }
}
