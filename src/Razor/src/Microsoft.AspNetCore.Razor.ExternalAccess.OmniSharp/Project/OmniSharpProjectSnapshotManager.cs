﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Document;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.OmniSharp.Project;

public class OmniSharpProjectSnapshotManager
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

    public IReadOnlyList<OmniSharpProjectSnapshot> Projects => InternalProjectSnapshotManager.Projects.Select(project => OmniSharpProjectSnapshot.Convert(project)!).ToList();

    public event EventHandler<OmniSharpProjectChangeEventArgs>? Changed;

    public OmniSharpProjectSnapshot GetLoadedProject(OmniSharpHostProject project)
    {
        return GetLoadedProject(project.InternalHostProject.FilePath);
    }

    public OmniSharpProjectSnapshot GetLoadedProject(string filePath)
    {
        var projectSnapshot = InternalProjectSnapshotManager.GetLoadedProject(filePath);
        // Forgiving null because we should only return null when projectSnapshot is null
        var converted = OmniSharpProjectSnapshot.Convert(projectSnapshot)!;

        return converted;
    }

    public void ProjectAdded(OmniSharpHostProject hostProject)
    {
        InternalProjectSnapshotManager.ProjectAdded(hostProject.InternalHostProject);
    }

    public void ProjectConfigurationChanged(OmniSharpHostProject hostProject)
    {
        InternalProjectSnapshotManager.ProjectConfigurationChanged(hostProject.InternalHostProject);
    }

    public void DocumentAdded(OmniSharpHostProject hostProject, OmniSharpHostDocument hostDocument)
    {
        var textLoader = _remoteTextLoaderFactory.Create(hostDocument.FilePath);
        InternalProjectSnapshotManager.DocumentAdded(hostProject.InternalHostProject, hostDocument.InternalHostDocument, textLoader);
    }

    public void DocumentChanged(string projectFilePath, string documentFilePath)
    {
        var textLoader = _remoteTextLoaderFactory.Create(documentFilePath);
        InternalProjectSnapshotManager.DocumentChanged(projectFilePath, documentFilePath, textLoader);
    }

    public void DocumentRemoved(OmniSharpHostProject hostProject, OmniSharpHostDocument hostDocument)
    {
        InternalProjectSnapshotManager.DocumentRemoved(hostProject.InternalHostProject, hostDocument.InternalHostDocument);
    }

    private void ProjectSnapshotManager_Changed(object? sender, ProjectChangeEventArgs args)
    {
        var convertedArgs = new OmniSharpProjectChangeEventArgs(args);
        Changed?.Invoke(this, convertedArgs);
    }
}
