// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.RpcContracts.Documents;
using System.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class DefaultProjectSnapshotManagerAccessor : ProjectSnapshotManagerAccessor, IDisposable
{
    private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
    private readonly IEnumerable<ProjectSnapshotChangeTrigger> _changeTriggers;
    private readonly IOptionsMonitor<RazorLSPOptions> _optionsMonitor;
    private readonly AdhocWorkspaceFactory _workspaceFactory;
    private readonly ClientNotifierServiceBase _notifierService;
    private ProjectSnapshotManagerBase? _instance;
    private bool _disposed;

    public DefaultProjectSnapshotManagerAccessor(
        ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
        IEnumerable<ProjectSnapshotChangeTrigger> changeTriggers,
        IOptionsMonitor<RazorLSPOptions> optionsMonitor,
        AdhocWorkspaceFactory workspaceFactory,
        ClientNotifierServiceBase notifier)
    {
        if (projectSnapshotManagerDispatcher is null)
        {
            throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
        }

        if (changeTriggers is null)
        {
            throw new ArgumentNullException(nameof(changeTriggers));
        }

        if (optionsMonitor is null)
        {
            throw new ArgumentNullException(nameof(optionsMonitor));
        }

        if (workspaceFactory is null)
        {
            throw new ArgumentNullException(nameof(workspaceFactory));
        }

        _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
        _changeTriggers = changeTriggers;
        _optionsMonitor = optionsMonitor;
        _workspaceFactory = workspaceFactory;
        _notifierService = notifier;
    }

    public override ProjectSnapshotManagerBase Instance
    {
        get
        {
            if (_instance is null)
            {
                var workspace = _workspaceFactory.Create(
                    workspaceServices: new IWorkspaceService[]
                    {
                        //PROTOTYPE: is this the right place to inject the service?
                        new RemoteProjectSnapshotProjectEngineFactory(_optionsMonitor)
                        , new LspRazorGeneratedDocumentProvider(_notifierService)
                    });
                _instance = new DefaultProjectSnapshotManager(
                    _projectSnapshotManagerDispatcher,
                    ErrorReporter.Instance,
                    _changeTriggers,
                    workspace);
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
