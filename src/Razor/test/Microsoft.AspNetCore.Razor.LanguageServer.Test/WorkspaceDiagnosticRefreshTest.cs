// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;
using Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Test;

public class WorkspaceDiagnosticRefreshTest(ITestOutputHelper testOutputHelper) : LanguageServerTestBase(testOutputHelper)
{
    [Fact]
    public async Task WorkspaceRefreshSent()
    {
        var projectSnapshotManager = CreateProjectSnapshotManager();
        var clientConnection = new Mock<IClientConnection>(MockBehavior.Strict);
        clientConnection
            .Setup(c => c.SendNotificationAsync(Methods.WorkspaceDiagnosticRefreshName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var publisher = new WorkspaceDiagnosticsRefresher(
            projectSnapshotManager,
            new TestClientCapabilitiesService(new()
            {
                Workspace = new()
                {
                    Diagnostics = new()
                    {
                        RefreshSupport = true
                    }
                }
            }),
            clientConnection.Object);

        var testAccessor = publisher.GetTestAccessor();

        await projectSnapshotManager.UpdateAsync(
            static updater =>
            {
                updater.CreateAndAddProject("C:/path/to/project.csproj");
            });

        await testAccessor.WaitForRefreshAsync();

        clientConnection.Verify();
    }

    [Fact]
    public async Task WorkspaceRefreshSent_MultipleTimes()
    {
        var projectSnapshotManager = CreateProjectSnapshotManager();
        var clientConnection = new Mock<IClientConnection>(MockBehavior.Strict);
        clientConnection
            .Verify(c => c.SendNotificationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                    Times.Exactly(2));

        var publisher = new WorkspaceDiagnosticsRefresher(
            projectSnapshotManager,
            new TestClientCapabilitiesService(new()
            {
                Workspace = new()
                {
                    Diagnostics = new()
                    {
                        RefreshSupport = true
                    }
                }
            }),
            clientConnection.Object);

        var testAccessor = publisher.GetTestAccessor();

        await projectSnapshotManager.UpdateAsync(
            static updater =>
            {
                updater.CreateAndAddProject("C:/path/to/project.csproj");
            });

        await testAccessor.WaitForRefreshAsync();

        await projectSnapshotManager.UpdateAsync(
            static updater =>
            {
                var project = (ProjectSnapshot)updater.GetProjects().Single();
                updater.CreateAndAddDocument(project, "C:/path/to/document.razor");
            });

        await testAccessor.WaitForRefreshAsync();

        clientConnection.Verify();
    }

    [Fact]
    public async Task WorkspaceRefreshNotSent_ClientDoesNotSupport()
    {
        var projectSnapshotManager = CreateProjectSnapshotManager();
        var clientConnection = new Mock<IClientConnection>(MockBehavior.Strict);
        clientConnection
            .Verify(c => c.SendNotificationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                    Times.Never);

        var publisher = new WorkspaceDiagnosticsRefresher(
            projectSnapshotManager,
            new TestClientCapabilitiesService(new()
            {
                Workspace = new()
                {
                    Diagnostics = new()
                    {
                        RefreshSupport = false
                    }
                }
            }),
            clientConnection.Object);

        var testAccessor = publisher.GetTestAccessor();

        await projectSnapshotManager.UpdateAsync(
            static updater =>
            {
                updater.CreateAndAddProject("C:/path/to/project.csproj");
            });

        await testAccessor.WaitForRefreshAsync();

        clientConnection.Verify();
    }
}
