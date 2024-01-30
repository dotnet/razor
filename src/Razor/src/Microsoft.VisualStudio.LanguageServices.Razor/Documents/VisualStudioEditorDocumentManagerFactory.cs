// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Editor.Razor.Documents;

[Shared]
[ExportWorkspaceServiceFactory(typeof(EditorDocumentManager), ServiceLayer.Host)]
internal class VisualStudioEditorDocumentManagerFactory : IWorkspaceServiceFactory
{
    private readonly SVsServiceProvider _serviceProvider;
    private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactory;
    private readonly IFileChangeTrackerFactory _fileChangeTrackerFactory;
    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    private readonly JoinableTaskContext _joinableTaskContext;

    [ImportingConstructor]
    public VisualStudioEditorDocumentManagerFactory(
        SVsServiceProvider serviceProvider,
        IVsEditorAdaptersFactoryService editorAdaptersFactory,
        IFileChangeTrackerFactory fileChangeTrackerFactory,
        JoinableTaskContext joinableTaskContext,
        ProjectSnapshotManagerDispatcher dispatcher)
    {
        _serviceProvider = serviceProvider;
        _editorAdaptersFactory = editorAdaptersFactory;
        _fileChangeTrackerFactory = fileChangeTrackerFactory;
        _joinableTaskContext = joinableTaskContext;
        _dispatcher = dispatcher;
    }

    public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
    {
        var runningDocumentTable = (IVsRunningDocumentTable)_serviceProvider.GetService(typeof(SVsRunningDocumentTable));
        return new VisualStudioEditorDocumentManager(
            _dispatcher, _joinableTaskContext, _fileChangeTrackerFactory, runningDocumentTable, _editorAdaptersFactory);
    }
}
