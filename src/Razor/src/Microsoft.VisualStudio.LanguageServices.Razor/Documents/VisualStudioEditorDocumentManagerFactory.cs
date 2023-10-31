// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
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
    private readonly JoinableTaskContext _joinableTaskContext;

    [ImportingConstructor]
    public VisualStudioEditorDocumentManagerFactory(
        SVsServiceProvider serviceProvider,
        IVsEditorAdaptersFactoryService editorAdaptersFactory,
        JoinableTaskContext joinableTaskContext)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _editorAdaptersFactory = editorAdaptersFactory ?? throw new ArgumentNullException(nameof(editorAdaptersFactory));
        _joinableTaskContext = joinableTaskContext ?? throw new ArgumentNullException(nameof(joinableTaskContext));
    }

    public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
    {
        if (workspaceServices is null)
        {
            throw new ArgumentNullException(nameof(workspaceServices));
        }

        var runningDocumentTable = (IVsRunningDocumentTable)_serviceProvider.GetService(typeof(SVsRunningDocumentTable));
        var fileChangeTrackerFactory = workspaceServices.GetRequiredService<FileChangeTrackerFactory>();
        return new VisualStudioEditorDocumentManager(
            _joinableTaskContext, fileChangeTrackerFactory, runningDocumentTable, _editorAdaptersFactory);
    }
}
