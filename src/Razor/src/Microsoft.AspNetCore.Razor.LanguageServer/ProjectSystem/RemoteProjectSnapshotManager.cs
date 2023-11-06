// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal sealed class RemoteProjectSnapshotManager : DefaultProjectSnapshotManager, IDisposable
{
    internal override Workspace Workspace { get; }
    private bool _disposed;

    public RemoteProjectSnapshotManager(
        IEnumerable<IProjectSnapshotChangeTrigger> changeTriggers,
        IProjectSnapshotProjectEngineFactory projectEngineFactory,
        AdhocWorkspaceFactory workspaceFactory,
        ProjectSnapshotManagerDispatcher dispatcher,
        IErrorReporter errorReporter)
        : base(errorReporter, changeTriggers, projectEngineFactory, dispatcher)
    {
        Workspace = workspaceFactory.Create();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Workspace.Dispose();
    }
}
