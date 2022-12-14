﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.LiveShare.Razor.Serialization;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;

namespace Microsoft.VisualStudio.LiveShare.Razor.Guest;

[ExportCollaborationService(typeof(ProjectSnapshotSynchronizationService), Scope = SessionScope.Guest)]
internal class ProjectSnapshotSynchronizationServiceFactory : ICollaborationServiceFactory
{
    private readonly ProxyAccessor _proxyAccessor;
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly Workspace _workspace;

    [ImportingConstructor]
    public ProjectSnapshotSynchronizationServiceFactory(
        ProxyAccessor proxyAccessor,
        JoinableTaskContext joinableTaskContext,
        [Import(typeof(VisualStudioWorkspace))] Workspace workspace)
    {
        if (proxyAccessor is null)
        {
            throw new ArgumentNullException(nameof(proxyAccessor));
        }

        if (joinableTaskContext is null)
        {
            throw new ArgumentNullException(nameof(joinableTaskContext));
        }

        if (workspace is null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        _proxyAccessor = proxyAccessor;
        _joinableTaskContext = joinableTaskContext;
        _workspace = workspace;
    }

    public async Task<ICollaborationService> CreateServiceAsync(CollaborationSession sessionContext, CancellationToken cancellationToken)
    {
        // This collaboration service depends on these serializers being immediately available so we need to register these now so the
        // guest project snapshot manager can retrieve the hosts project state.
        var serializer = (JsonSerializer)sessionContext.GetService(typeof(JsonSerializer));
        serializer.Converters.RegisterRazorLiveShareConverters();

        var languageServices = _workspace.Services.GetLanguageServices(RazorLanguage.Name);
        var projectManager = (ProjectSnapshotManagerBase)languageServices.GetRequiredService<ProjectSnapshotManager>();

        var projectSnapshotManagerProxy = await sessionContext.GetRemoteServiceAsync<IProjectSnapshotManagerProxy>(typeof(IProjectSnapshotManagerProxy).Name, cancellationToken);
        var synchronizationService = new ProjectSnapshotSynchronizationService(
            _joinableTaskContext.Factory,
            sessionContext,
            projectSnapshotManagerProxy,
            projectManager);

        await synchronizationService.InitializeAsync(cancellationToken);

        return synchronizationService;
    }
}
