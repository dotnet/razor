// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor.Documents;

[Shared]
[ExportWorkspaceServiceFactory(typeof(FileChangeTrackerFactory), ServiceLayer.Host)]
internal class VisualStudioFileChangeTrackerFactoryFactory : IWorkspaceServiceFactory
{
    private readonly IVsAsyncFileChangeEx? _fileChangeService;
    private readonly IProjectSnapshotManagerDispatcher _dispatcher;
    private readonly JoinableTaskContext _joinableTaskContext;

    [ImportingConstructor]
    public VisualStudioFileChangeTrackerFactoryFactory(
        SVsServiceProvider serviceProvider,
        IProjectSnapshotManagerDispatcher dispatcher,
        JoinableTaskContext joinableTaskContext)
    {
        if (serviceProvider is null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        if (dispatcher is null)
        {
            throw new ArgumentNullException(nameof(dispatcher));
        }

        if (joinableTaskContext is null)
        {
            throw new ArgumentNullException(nameof(joinableTaskContext));
        }

        _fileChangeService = serviceProvider.GetService(typeof(SVsFileChangeEx)) as IVsAsyncFileChangeEx;
        _dispatcher = dispatcher;
        _joinableTaskContext = joinableTaskContext;
    }

    public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
    {
        if (workspaceServices is null)
        {
            throw new ArgumentNullException(nameof(workspaceServices));
        }

        var errorReporter = workspaceServices.GetRequiredService<IErrorReporter>();
        return new VisualStudioFileChangeTrackerFactory(errorReporter, _fileChangeService!, _dispatcher, _joinableTaskContext);
    }
}
