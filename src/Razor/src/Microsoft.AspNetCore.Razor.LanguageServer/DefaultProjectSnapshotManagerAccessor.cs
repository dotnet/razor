// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class DefaultProjectSnapshotManagerAccessor : ProjectSnapshotManagerAccessor, IDisposable
{
    private readonly IEnumerable<IProjectSnapshotChangeTrigger> _changeTriggers;
    private readonly IProjectSnapshotProjectEngineFactory _projectEngineFactory;
    private readonly AdhocWorkspaceFactory _workspaceFactory;
    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    private readonly IErrorReporter _errorReporter;

    private ProjectSnapshotManagerBase? _instance;
    private bool _disposed;

    public DefaultProjectSnapshotManagerAccessor(
        IEnumerable<IProjectSnapshotChangeTrigger> changeTriggers,
        IProjectSnapshotProjectEngineFactory projectEngineFactory,
        AdhocWorkspaceFactory workspaceFactory,
        ProjectSnapshotManagerDispatcher dispatcher,
        IErrorReporter errorReporter)
    {
        _changeTriggers = changeTriggers;
        _projectEngineFactory = projectEngineFactory;
        _workspaceFactory = workspaceFactory;
        _dispatcher = dispatcher;
        _errorReporter = errorReporter;
    }

    public override ProjectSnapshotManagerBase Instance
    {
        get
        {
            if (_instance is null)
            {
                var workspace = _workspaceFactory.Create();
                _instance = new DefaultProjectSnapshotManager(
                    _errorReporter,
                    _changeTriggers,
                    _projectEngineFactory,
                    workspace,
                    _dispatcher);
            }

            return _instance;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _instance?.Workspace.Dispose();
    }
}
