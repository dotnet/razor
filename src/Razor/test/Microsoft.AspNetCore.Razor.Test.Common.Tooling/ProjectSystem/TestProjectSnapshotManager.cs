// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;

internal class TestProjectSnapshotManager : DefaultProjectSnapshotManager
{
    public bool AllowNotifyListeners { get; set; }
    public ProjectChangeKind? ListenersNotifiedOf { get; private set; }

    public TestProjectSnapshotManager(ProjectSnapshotManagerDispatcher dispatcher)
       : this(triggers: [], ProjectEngineFactories.DefaultProvider, dispatcher, StrictMock.Of<IErrorReporter>())
    {
    }

    public TestProjectSnapshotManager(
        IProjectEngineFactoryProvider projectEngineFactoryProvider,
        ProjectSnapshotManagerDispatcher dispatcher)
        : this(triggers: [], projectEngineFactoryProvider, dispatcher, StrictMock.Of<IErrorReporter>())
    {
    }

    public TestProjectSnapshotManager(
        IProjectSnapshotChangeTrigger[] triggers,
        IProjectEngineFactoryProvider projectEngineFactoryProvider,
        ProjectSnapshotManagerDispatcher dispatcher)
        : this(triggers, projectEngineFactoryProvider, dispatcher, StrictMock.Of<IErrorReporter>())
    {
    }

    public TestProjectSnapshotManager(
        IProjectSnapshotChangeTrigger[] triggers,
        IProjectEngineFactoryProvider projectEngineFactoryProvider,
        ProjectSnapshotManagerDispatcher dispatcher,
        IErrorReporter errorReporter)
        : base(triggers, projectEngineFactoryProvider, dispatcher, errorReporter)
    {
    }

    public static TestProjectSnapshotManager Create(ProjectSnapshotManagerDispatcher dispatcher, IErrorReporter errorReporter)
        => new TestProjectSnapshotManager(triggers: [], ProjectEngineFactories.DefaultProvider, dispatcher, errorReporter);

    public IProjectSnapshotManagerAccessor GetAccessor()
    {
        var mock = new StrictMock<IProjectSnapshotManagerAccessor>();

        mock.SetupGet(x => x.Instance)
            .Returns(this);

        ProjectSnapshotManagerBase? @this = this;
        mock.Setup(x => x.TryGetInstance(out @this))
            .Returns(true);

        return mock.Object;
    }

    public TestDocumentSnapshot CreateAndAddDocument(ProjectSnapshot projectSnapshot, string filePath)
    {
        var documentSnapshot = TestDocumentSnapshot.Create(projectSnapshot, filePath);
        DocumentAdded(projectSnapshot.Key, documentSnapshot.HostDocument, new DocumentSnapshotTextLoader(documentSnapshot));

        return documentSnapshot;
    }

    internal TestProjectSnapshot CreateAndAddProject(string filePath)
    {
        var projectSnapshot = TestProjectSnapshot.Create(filePath);
        ProjectAdded(projectSnapshot.HostProject);

        return projectSnapshot;
    }

    public IProjectSnapshot? FindProject(HostProject hostProject)
    {
        return GetProjects().FirstOrDefault(s => s.FilePath == hostProject.FilePath);
    }

    public void Reset()
    {
        ListenersNotifiedOf = null;
    }

    protected override void NotifyListeners(ProjectChangeEventArgs e)
    {
        ListenersNotifiedOf = e.Kind;

        if (AllowNotifyListeners)
        {
            base.NotifyListeners(e);
        }
    }

    private sealed class TestWorkspaceProvider(Workspace workspace) : IWorkspaceProvider
    {
        public Workspace GetWorkspace() => workspace;
    }
}
