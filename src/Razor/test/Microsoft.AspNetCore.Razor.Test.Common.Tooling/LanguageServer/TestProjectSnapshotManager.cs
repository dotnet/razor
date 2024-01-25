// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;

internal class TestProjectSnapshotManager : DefaultProjectSnapshotManager
{
    private TestProjectSnapshotManager(
        Workspace workspace,
        IProjectEngineFactoryProvider projectEngineFactoryProvider,
        ProjectSnapshotManagerDispatcher dispatcher,
        IErrorReporter errorReporter)
        : base(triggers: [], workspace, projectEngineFactoryProvider, dispatcher, errorReporter)
    {
    }

    public static TestProjectSnapshotManager Create(ProjectSnapshotManagerDispatcher dispatcher, IErrorReporter errorReporter)
    {
        var services = TestServices.Create(workspaceServices: [], razorLanguageServices: []);
        var workspace = TestWorkspace.Create(services);
        var testProjectManager = new TestProjectSnapshotManager(workspace, ProjectEngineFactories.DefaultProvider, dispatcher, errorReporter);

        return testProjectManager;
    }

    public bool AllowNotifyListeners { get; set; }

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

    protected override void NotifyListeners(ProjectChangeEventArgs e)
    {
        if (AllowNotifyListeners)
        {
            base.NotifyListeners(e);
        }
    }
}
