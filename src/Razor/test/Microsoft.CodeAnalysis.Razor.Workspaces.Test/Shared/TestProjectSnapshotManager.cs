// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Linq;
using Moq;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal class TestProjectSnapshotManager(
    IProjectSnapshotProjectEngineFactory projectEngineFactory,
    Workspace workspace,
    ProjectSnapshotManagerDispatcher dispatcher)
    : DefaultProjectSnapshotManager(
        Mock.Of<IErrorReporter>(MockBehavior.Strict),
        Enumerable.Empty<IProjectSnapshotChangeTrigger>(),
        projectEngineFactory,
        dispatcher)
{
    internal override Workspace Workspace { get; } = workspace;

    public bool AllowNotifyListeners { get; set; }

    public ProjectSnapshot? GetSnapshot(HostProject hostProject)
    {
        return GetProjects().Cast<ProjectSnapshot>().FirstOrDefault(s => s.FilePath == hostProject.FilePath);
    }

    public ProjectSnapshot? GetSnapshot(Project workspaceProject)
    {
        return GetProjects().Cast<ProjectSnapshot>().FirstOrDefault(s => s.FilePath == workspaceProject.FilePath);
    }

    protected override void NotifyListeners(ProjectChangeEventArgs e)
    {
        if (AllowNotifyListeners)
        {
            base.NotifyListeners(e);
        }
    }
}
