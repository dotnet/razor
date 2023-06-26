// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.ProjectSystem;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal abstract class ProjectSnapshotManagerBase : ProjectSnapshotManager
{
    internal abstract Workspace Workspace { get; }

    internal abstract IErrorReporter ErrorReporter { get; }

    internal abstract ImmutableArray<string> GetOpenDocuments();

    internal abstract void DocumentAdded(HostProject hostProject, HostDocument hostDocument, TextLoader textLoader);

    internal abstract void DocumentOpened(string projectFilePath, string documentFilePath, SourceText sourceText);

    internal abstract void DocumentClosed(string projectFilePath, string documentFilePath, TextLoader textLoader);

    internal abstract void DocumentChanged(string projectFilePath, string documentFilePath, TextLoader textLoader);

    internal abstract void DocumentChanged(string projectFilePath, string documentFilePath, SourceText sourceText);

    internal abstract void DocumentRemoved(HostProject hostProject, HostDocument hostDocument);

    internal abstract void ProjectAdded(HostProject hostProject);

    internal abstract void ProjectConfigurationChanged(HostProject hostProject);

    internal abstract void ProjectWorkspaceStateChanged(string projectFilePath, ProjectWorkspaceState? projectWorkspaceState);

    internal abstract void ProjectRemoved(HostProject hostProject);

    internal abstract void ReportError(Exception exception);

    internal abstract void ReportError(Exception exception, IProjectSnapshot project);

    internal abstract void ReportError(Exception exception, HostProject hostProject);

    internal abstract void SolutionOpened();

    internal abstract void SolutionClosed();

    /// <summary>
    /// Gets a project if it's already loaded, or calls <see cref="ProjectAdded(HostProject)" /> with a new host project
    /// </summary>
    internal abstract IProjectSnapshot GetOrAddLoadedProject(string normalizedPath, RazorConfiguration configuration, string? rootNamespace);

    internal abstract bool TryRemoveLoadedProject(string normalizedPath, [NotNullWhen(true)] out IProjectSnapshot? project);

    internal abstract void UpdateProject(
        string normalizedPath,
        RazorConfiguration configuration,
        ProjectWorkspaceState projectWorkspaceState,
        string? rootNamespace,
        Func<IProjectSnapshot, ImmutableArray<IUpdateProjectAction>> calculateUpdates);
}
