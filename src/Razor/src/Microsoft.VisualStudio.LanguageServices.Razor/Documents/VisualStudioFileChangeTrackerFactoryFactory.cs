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
    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    private readonly JoinableTaskContext _joinableTaskContext;
    private readonly IErrorReporter _errorReporter;

    [ImportingConstructor]
    public VisualStudioFileChangeTrackerFactoryFactory(
        SVsServiceProvider serviceProvider,
        ProjectSnapshotManagerDispatcher dispatcher,
        JoinableTaskContext joinableTaskContext,
        IErrorReporter errorReporter)
    {
        if (serviceProvider is null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        _fileChangeService = serviceProvider.GetService(typeof(SVsFileChangeEx)) as IVsAsyncFileChangeEx;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));
        _errorReporter = errorReporter ?? throw new ArgumentNullException(nameof(errorReporter));
    }

    public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
    {
        return new VisualStudioFileChangeTrackerFactory(_errorReporter, _fileChangeService!, _dispatcher, _joinableTaskContext);
    }
}
