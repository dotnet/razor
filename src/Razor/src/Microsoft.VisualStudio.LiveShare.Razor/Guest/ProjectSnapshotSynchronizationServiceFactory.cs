// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LiveShare.Razor.Serialization;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;

namespace Microsoft.VisualStudio.LiveShare.Razor.Guest;

[ExportCollaborationService(typeof(ProjectSnapshotSynchronizationService), Scope = SessionScope.Guest)]
internal class ProjectSnapshotSynchronizationServiceFactory : ICollaborationServiceFactory
{
    private readonly ProxyAccessor _proxyAccessor;
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly ProjectSnapshotManagerBase _projectManager;

    [ImportingConstructor]
    public ProjectSnapshotSynchronizationServiceFactory(
        ProxyAccessor proxyAccessor,
        JoinableTaskContext joinableTaskContext,
        ProjectSnapshotManager projectManager)
    {
        _proxyAccessor = proxyAccessor;
        _joinableTaskContext = joinableTaskContext;
        _projectManager = (ProjectSnapshotManagerBase)projectManager;
    }

    public async Task<ICollaborationService> CreateServiceAsync(CollaborationSession sessionContext, CancellationToken cancellationToken)
    {
        // This collaboration service depends on these serializers being immediately available so we need to register these now so the
        // guest project snapshot manager can retrieve the hosts project state.
        var serializer = (JsonSerializer)sessionContext.GetService(typeof(JsonSerializer));
        serializer.Converters.RegisterRazorLiveShareConverters();

        var projectSnapshotManagerProxy = await sessionContext.GetRemoteServiceAsync<IProjectSnapshotManagerProxy>(typeof(IProjectSnapshotManagerProxy).Name, cancellationToken);
        var synchronizationService = new ProjectSnapshotSynchronizationService(
            _joinableTaskContext.Factory,
            sessionContext,
            projectSnapshotManagerProxy,
            _projectManager);

        await synchronizationService.InitializeAsync(cancellationToken);

        return synchronizationService;
    }
}
