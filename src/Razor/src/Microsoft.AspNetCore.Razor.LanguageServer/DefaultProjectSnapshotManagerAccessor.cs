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
using System.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class LspRazorGeneratedDocumentProvider : IRazorGeneratedDocumentProvider
{
    readonly ClientNotifierServiceBase _notifier;

    public LspRazorGeneratedDocumentProvider(ClientNotifierServiceBase notifier)
    {
        _notifier = notifier;
    }

    public async Task<(string CSharp, string Html, string Json)> GetGeneratedDocumentAsync(IDocumentSnapshot documentSnapshot)
    {
        var projectRoot = documentSnapshot.Project.FilePath.Substring(0, documentSnapshot.Project.FilePath.LastIndexOf("/"));
        var documentName = GetIdentifierFromPath(documentSnapshot.FilePath?.Substring(projectRoot.Length + 1) ?? "");

        var csharp = await RequestOutput(documentName + ".rsg.cs");
        var html = await RequestOutput(documentName + ".rsg.html");
        var json = await RequestOutput(documentName + ".rsg.json");


        return (csharp, html, json);

        async Task<string> RequestOutput(string name)
        {
            var request = new HostOutputRequest()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = new UriBuilder()
                    {
                        Scheme = Uri.UriSchemeFile,
                        Path = documentSnapshot.FilePath,
                        Host = string.Empty,
                    }.Uri
                },
                GeneratorName = "Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator",
                RequestedOutput = name,
            };

            var response = await _notifier.SendRequestAsync<HostOutputRequest, HostOutputResponse>(RazorLanguageServerCustomMessageTargets.RazorHostOutputsEndpointName, request, CancellationToken.None);
            return response.Output ?? string.Empty;
        }
    }

    //copied from the generator
    internal static string GetIdentifierFromPath(string filePath)
    {
        var builder = new StringBuilder(filePath.Length);

        for (var i = 0; i < filePath.Length; i++)
        {
            switch (filePath[i])
            {
                case ':' or '\\' or '/':
                case char ch when !char.IsLetterOrDigit(ch):
                    builder.Append('_');
                    break;
                default:
                    builder.Append(filePath[i]);
                    break;
            }
        }

        return builder.ToString();
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

        //Microsoft.CodeAnalysis.CodeAnalysisEventSource.Log.Message("project accessor made. notifier is :" + (notifier is not null));
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
