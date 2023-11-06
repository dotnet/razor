// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LiveShare.Razor.Serialization;
using Microsoft.VisualStudio.Threading;
using Newtonsoft.Json;

namespace Microsoft.VisualStudio.LiveShare.Razor.Host;

[ExportCollaborationService(
    typeof(IProjectSnapshotManagerProxy),
    Name = nameof(IProjectSnapshotManagerProxy),
    Scope = SessionScope.Host,
    Role = ServiceRole.RemoteService)]
[method: ImportingConstructor]
internal class DefaultProjectSnapshotManagerProxyFactory(
    ProjectSnapshotManagerDispatcher dispatcher,
    JoinableTaskContext joinableTaskContext,
    ProjectSnapshotManager projectManager) : ICollaborationServiceFactory
{
    private readonly ProjectSnapshotManagerDispatcher _dispatcher = dispatcher;
    private readonly JoinableTaskContext _joinableTaskContext = joinableTaskContext;
    private readonly ProjectSnapshotManager _projectManager = projectManager;

    public Task<ICollaborationService> CreateServiceAsync(CollaborationSession session, CancellationToken cancellationToken)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        var serializer = (JsonSerializer)session.GetService(typeof(JsonSerializer));
        serializer.Converters.RegisterRazorLiveShareConverters();

        var service = new DefaultProjectSnapshotManagerProxy(session, _dispatcher, _projectManager, _joinableTaskContext.Factory);
        return Task.FromResult<ICollaborationService>(service);
    }
}
