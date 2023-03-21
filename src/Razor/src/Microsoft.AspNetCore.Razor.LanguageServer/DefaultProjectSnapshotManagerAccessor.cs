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
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Remote;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.RpcContracts.Documents;

namespace Microsoft.AspNetCore.Razor.LanguageServer;


//internal class SnapshotHandler : IRazorRequestHandler<GetHostOutputRequest, GetHostOutputResponse>
//{
//    public bool MutatesSolutionState => false;

//    public TextDocumentIdentifier GetTextDocumentIdentifier(GetHostOutputRequest request)
//    {
//        throw new NotImplementedException();
//    }

//    public Task<GetHostOutputResponse> HandleRequestAsync(GetHostOutputRequest request, RazorRequestContext context, CancellationToken cancellationToken)
//    {
//        throw new NotImplementedException();
//    }
//}



internal class LspHostOutput : IGeneratorSnapshotProvider
{
    ClientNotifierServiceBase _notifier;

    public LspHostOutput(ClientNotifierServiceBase notifier)
    {
        _notifier = notifier;
    }

    public async Task GetGenerateDocumentsAsync(IDocumentSnapshot documentSnapshot)
    {
        // ask the LSP for the generated doc based on file path (and other stuff)
        var request = new GetHostOutputRequest()
        {
            TextDocument = new TextDocumentIdentifier()
            {
                Uri = new UriBuilder()
                {
                    Scheme = Uri.UriSchemeFile,
                    Path = documentSnapshot.FilePath,
                    Host = string.Empty,
                }.Uri
            }
        };

        var response = await _notifier.SendRequestAsync<GetHostOutputRequest, GetHostOutputResponse>(RazorLanguageServerCustomMessageTargets.RazorHostOutputsEndpointName, request, CancellationToken.None);
        //Microsoft.CodeAnalysis.CodeAnalysisEventSource.Log.Message("Response to get gen was:" + response.Response);
    }
}

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
        //_generatorSnapshotFactory = snapshotFactory;

        Microsoft.CodeAnalysis.CodeAnalysisEventSource.Log.Message("project accessor made. notifier is :" + (notifier is not null));
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
                        //PROTOTYPE: it's here we could inject a 'host outputs retrieval' service
                        new RemoteProjectSnapshotProjectEngineFactory(_optionsMonitor)
                        , new LspHostOutput(_notifierService)
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
