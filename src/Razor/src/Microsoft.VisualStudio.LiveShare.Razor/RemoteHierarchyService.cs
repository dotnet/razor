﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.LiveShare.Razor;

internal class RemoteHierarchyService : IRemoteHierarchyService
{
    private readonly CollaborationSession _session;
    private readonly JoinableTaskFactory _joinableTaskFactory;

    internal RemoteHierarchyService(CollaborationSession session, JoinableTaskFactory joinableTaskFactory)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (joinableTaskFactory is null)
        {
            throw new ArgumentNullException(nameof(joinableTaskFactory));
        }

        _session = session;
        _joinableTaskFactory = joinableTaskFactory;
    }

    public async Task<bool> HasCapabilityAsync(Uri pathOfFileInProject, string capability, CancellationToken cancellationToken)
    {
        if (pathOfFileInProject is null)
        {
            throw new ArgumentNullException(nameof(pathOfFileInProject));
        }

        if (capability is null)
        {
            throw new ArgumentNullException(nameof(capability));
        }

        await _joinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        var hostPathOfFileInProject = _session.ConvertSharedUriToLocalPath(pathOfFileInProject);
        if (ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShellOpenDocument)) is not IVsUIShellOpenDocument vsUIShellOpenDocument)
        {
            return false;
        }

        var hr = vsUIShellOpenDocument.IsDocumentInAProject(hostPathOfFileInProject, out var hierarchy, out _, out _, out _);
        if (!ErrorHandler.Succeeded(hr) || hierarchy is null)
        {
            return false;
        }

        try
        {
            var isCapabilityMatch = hierarchy.IsCapabilityMatch(capability);
            return isCapabilityMatch;
        }
        catch (NotSupportedException)
        {
            // IsCapabilityMatch throws a NotSupportedException if it can't create a
            // BooleanSymbolExpressionEvaluator COM object
        }
        catch (ObjectDisposedException)
        {
            // IsCapabilityMatch throws an ObjectDisposedException if the underlying
            //    hierarchy has been disposed (Bug 253462)
        }

        return false;
    }
}
