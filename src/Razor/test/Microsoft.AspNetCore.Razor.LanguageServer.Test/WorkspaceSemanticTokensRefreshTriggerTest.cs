﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

public class WorkspaceSemanticTokensRefreshTriggerTest : LanguageServerTestBase
{
    private static readonly HostProject s_hostProject = new("/path/to/project.csproj", "/path/to/obj", RazorConfiguration.Default, "TestRootNamespace");
    private static readonly HostDocument s_hostDocument = new("/path/to/file.razor", "file.razor");

    private readonly TestProjectSnapshotManager _projectManager;

    public WorkspaceSemanticTokensRefreshTriggerTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _projectManager = TestProjectSnapshotManager.Create(Dispatcher, ErrorReporter);
        _projectManager.AllowNotifyListeners = true;
    }

    protected override async Task InitializeAsync()
    {
        await RunOnDispatcherAsync(() =>
        {
            _projectManager.ProjectAdded(s_hostProject);
            _projectManager.DocumentAdded(s_hostProject.Key, s_hostDocument, new EmptyTextLoader(s_hostDocument.FilePath));
        });
    }

    [Fact]
    public async Task PublishesOnWorkspaceUpdate()
    {
        // Arrange
        var publisher = new StrictMock<IWorkspaceSemanticTokensRefreshPublisher>();
        publisher
            .Setup(w => w.EnqueueWorkspaceSemanticTokensRefresh())
            .Verifiable();

        var refreshTrigger = new TestWorkspaceSemanticTokensRefreshTrigger(publisher.Object);
        refreshTrigger.Initialize(_projectManager);

        // Act
        var newDocument = new HostDocument("/path/to/newFile.razor", "newFile.razor");

        await RunOnDispatcherAsync(() =>
            _projectManager.DocumentAdded(s_hostProject.Key, newDocument, new EmptyTextLoader(newDocument.FilePath)));

        // Assert
        publisher.VerifyAll();
    }

    private class TestWorkspaceSemanticTokensRefreshTrigger(IWorkspaceSemanticTokensRefreshPublisher publisher)
        : WorkspaceSemanticTokensRefreshTrigger(publisher)
    {
    }
}
