﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.OmniSharpPlugin;

internal abstract class OmniSharpProjectSnapshotManagerBase : OmniSharpProjectSnapshotManager
{
    internal abstract ProjectSnapshotManagerBase InternalProjectSnapshotManager { get; }

    public abstract Workspace Workspace { get; }

    public abstract void ProjectAdded(OmniSharpHostProject hostProject);

    public abstract void DocumentAdded(OmniSharpHostProject hostProject, OmniSharpHostDocument hostDocument);

    public abstract void DocumentChanged(string projectFilePath, string documentFilePath);

    public abstract void DocumentRemoved(OmniSharpHostProject hostProject, OmniSharpHostDocument hostDocument);

    public abstract void ProjectRemoved(OmniSharpHostProject hostProject);

    public abstract void ProjectConfigurationChanged(OmniSharpHostProject hostProject);

    public abstract void ProjectWorkspaceStateChanged(string projectFilePath, ProjectWorkspaceState projectWorkspaceState);
}
