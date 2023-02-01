// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Document;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Project;

internal class OmniSharpProjectSnapshotManager
{
    private readonly RemoteTextLoaderFactory _remoteTextLoaderFactory;

    internal OmniSharpProjectSnapshotManager(
        ProjectSnapshotManagerBase projectSnapshotManager,
        RemoteTextLoaderFactory remoteTextLoaderFactory)
    {
        if (projectSnapshotManager is null)
        {
            throw new ArgumentNullException(nameof(projectSnapshotManager));
        }

        if (remoteTextLoaderFactory is null)
        {
            throw new ArgumentNullException(nameof(remoteTextLoaderFactory));
        }

        InternalProjectSnapshotManager = projectSnapshotManager;
        _remoteTextLoaderFactory = remoteTextLoaderFactory;
        InternalProjectSnapshotManager.Changed += ProjectSnapshotManager_Changed;
    }

    internal ProjectSnapshotManagerBase InternalProjectSnapshotManager { get; }

    internal Workspace Workspace => InternalProjectSnapshotManager.Workspace;

    internal IReadOnlyList<OmniSharpProjectSnapshot> Projects => InternalProjectSnapshotManager.Projects.Select(project => OmniSharpProjectSnapshot.Convert(project)).ToList();

    internal event EventHandler<OmniSharpProjectChangeEventArgs> Changed;

    internal OmniSharpProjectSnapshot GetLoadedProject(string filePath)
    {
        var projectSnapshot = InternalProjectSnapshotManager.GetLoadedProject(filePath);
        var converted = OmniSharpProjectSnapshot.Convert(projectSnapshot);

        return converted;
    }

    internal void ProjectAdded(OmniSharpHostProject hostProject)
    {
        InternalProjectSnapshotManager.ProjectAdded(hostProject.InternalHostProject);
    }

    internal void ProjectRemoved(OmniSharpHostProject hostProject)
    {
        InternalProjectSnapshotManager.ProjectRemoved(hostProject.InternalHostProject);
    }

    internal void ProjectConfigurationChanged(OmniSharpHostProject hostProject)
    {
        InternalProjectSnapshotManager.ProjectConfigurationChanged(hostProject.InternalHostProject);
    }

    internal void ProjectWorkspaceStateChanged(string projectFilePath, ProjectWorkspaceState projectWorkspaceState)
    {
        InternalProjectSnapshotManager.ProjectWorkspaceStateChanged(projectFilePath, projectWorkspaceState);
    }

    internal void DocumentAdded(OmniSharpHostProject hostProject, OmniSharpHostDocument hostDocument)
    {
        var textLoader = _remoteTextLoaderFactory.Create(hostDocument.FilePath);
        InternalProjectSnapshotManager.DocumentAdded(hostProject.InternalHostProject, hostDocument.InternalHostDocument, textLoader);
    }

    internal void DocumentChanged(string projectFilePath, string documentFilePath)
    {
        var textLoader = _remoteTextLoaderFactory.Create(documentFilePath);
        InternalProjectSnapshotManager.DocumentChanged(projectFilePath, documentFilePath, textLoader);
    }

    internal void DocumentRemoved(OmniSharpHostProject hostProject, OmniSharpHostDocument hostDocument)
    {
        InternalProjectSnapshotManager.DocumentRemoved(hostProject.InternalHostProject, hostDocument.InternalHostDocument);
    }

    private void ProjectSnapshotManager_Changed(object sender, ProjectChangeEventArgs args)
    {
        var convertedArgs = new OmniSharpProjectChangeEventArgs(args);
        Changed?.Invoke(this, convertedArgs);
    }
}
