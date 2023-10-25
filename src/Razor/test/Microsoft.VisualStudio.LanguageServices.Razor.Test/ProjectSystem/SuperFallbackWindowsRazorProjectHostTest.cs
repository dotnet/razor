// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ItemCollection = Microsoft.VisualStudio.ProjectSystem.ItemCollection;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

public class SuperFallbackWindowsRazorProjectHostTest : ProjectSnapshotManagerDispatcherWorkspaceTestBase
{
    private readonly TestProjectSnapshotManager _projectManager;
    private readonly ProjectConfigurationFilePathStore _projectConfigurationFilePathStore;
    private readonly ItemCollection _noneItems;

    public SuperFallbackWindowsRazorProjectHostTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _projectManager = new TestProjectSnapshotManager(Workspace, Dispatcher);

        var projectConfigurationFilePathStore = new Mock<ProjectConfigurationFilePathStore>(MockBehavior.Strict);
        projectConfigurationFilePathStore.Setup(s => s.Remove(It.IsAny<ProjectKey>())).Verifiable();
        _projectConfigurationFilePathStore = projectConfigurationFilePathStore.Object;

        _noneItems = new ItemCollection(ManagedProjectSystemSchema.NoneItem.SchemaName);
    }

    [Fact]
    public void GetChangedAndRemovedDocuments_ReturnsChangedContentAndNoneItems()
    {
        // Arrange
        var afterChangeNoneItems = new ItemCollection(ManagedProjectSystemSchema.NoneItem.SchemaName);
        _noneItems.Item("About.cshtml", new Dictionary<string, string>());
        var services = new TestProjectSystemServices("C:\\Project\\Test.csproj");

        var host = new TestSuperFallbackWindowsRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);
        var changes = new TestProjectChangeDescription[]
        {
             afterChangeNoneItems.ToChange(_noneItems.ToSnapshot()),
        };
        var update = services.CreateUpdate(changes).Value;

        // Act
        var result = host.GetChangedAndRemovedDocuments(update);

        // Assert
        Assert.Collection(
            result,
            document =>
            {
                Assert.Equal("C:\\Project\\About.cshtml", document.FilePath);
                Assert.Equal("About.cshtml", document.TargetPath);
            });
    }

    [Fact]
    public void GetCurrentDocuments_ReturnsContentAndNoneItems()
    {
        // Arrange
        _noneItems.Item("About.cshtml", new Dictionary<string, string>());
        var services = new TestProjectSystemServices("C:\\Project\\Test.csproj");

        var host = new TestSuperFallbackWindowsRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);
        var changes = new TestProjectChangeDescription[]
        {
             _noneItems.ToChange(),
        };
        var update = services.CreateUpdate(changes).Value;

        // Act
        var result = host.GetCurrentDocuments(update);

        // Assert
        Assert.Collection(
            result,
            document =>
            {
                Assert.Equal("C:\\Project\\About.cshtml", document.FilePath);
                Assert.Equal("About.cshtml", document.TargetPath);
            });
    }

    [Fact]
    public void TryGetRazorDocument_NonRazorFilePath_ReturnsFalse()
    {
        // Arrange
        var services = new TestProjectSystemServices("C:\\Path\\Test.csproj");

        var host = new TestSuperFallbackWindowsRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);

        // Act
        var result = host.TryGetRazorDocument("site.css", out var document);

        // Assert
        Assert.False(result);
        Assert.Null(document);
    }

    [Fact]
    public void TryGetRazorDocument_Cshtml_SetsLegacyFileKind()
    {
        // Arrange
        var expectedFullPath = "C:\\Project\\Index.cshtml";
        var expectedTargetPath = "Index.cshtml";
        var services = new TestProjectSystemServices("C:\\Project\\Test.csproj");

        var host = new TestSuperFallbackWindowsRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);

        // Act
        var result = host.TryGetRazorDocument("Index.cshtml", out var document);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedFullPath, document.FilePath);
        Assert.Equal(expectedTargetPath, document.TargetPath);
        Assert.Equal(FileKinds.Legacy, document.FileKind);
    }

    [Fact]
    public void TryGetRazorDocument_Razor_SetsLegacyFileKind()
    {
        // Arrange
        var expectedFullPath = "C:\\Project\\Index.razor";
        var expectedTargetPath = "Index.razor";
        var services = new TestProjectSystemServices("C:\\Project\\Test.csproj");

        var host = new TestSuperFallbackWindowsRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);

        // Act
        var result = host.TryGetRazorDocument("Index.razor", out var document);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedFullPath, document.FilePath);
        Assert.Equal(expectedTargetPath, document.TargetPath);
        Assert.Equal(FileKinds.Component, document.FileKind);
    }

    [UIFact]
    public async Task FallbackRazorProjectHost_UIThread_CreateAndDispose_Succeeds()
    {
        // Arrange
        var services = new TestProjectSystemServices("C:\\To\\Test.csproj");

        var host = new TestSuperFallbackWindowsRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);

        // Act & Assert
        await host.LoadAsync();
        Assert.Empty(_projectManager.GetProjects());

        await host.DisposeAsync();
        Assert.Empty(_projectManager.GetProjects());
    }

    [UIFact]
    public async Task FallbackRazorProjectHost_BackgroundThread_CreateAndDispose_Succeeds()
    {
        // Arrange
        var services = new TestProjectSystemServices("Test.csproj");

        var host = new TestSuperFallbackWindowsRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);

        // Act & Assert
        await Task.Run(async () => await host.LoadAsync());
        Assert.Empty(_projectManager.GetProjects());

        await Task.Run(async () => await host.DisposeAsync());
        Assert.Empty(_projectManager.GetProjects());
    }

    [UIFact] // This can happen if the .xaml files aren't included correctly.
    public async Task OnProjectChanged_NoRulesDefined()
    {
        // Arrange
        var changes = new TestProjectChangeDescription[]
        {
        };

        var services = new TestProjectSystemServices("Test.csproj");

        var host = new TestSuperFallbackWindowsRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);

        // Act & Assert
        await Task.Run(async () => await host.LoadAsync());
        Assert.Empty(_projectManager.GetProjects());

        await Task.Run(async () => await host.OnProjectChangedAsync(string.Empty, services.CreateUpdate(changes)));
        Assert.Empty(_projectManager.GetProjects());
    }

    [UIFact]
    public async Task OnProjectChanged_NoDocuments_DoesNotInitializeProject()
    {
        _noneItems.Item("About.cs", new Dictionary<string, string>());

        // Arrange
        var changes = new TestProjectChangeDescription[]
        {
             _noneItems.ToChange(),
        };
        var services = new TestProjectSystemServices("Test.csproj");

        var host = new TestSuperFallbackWindowsRazorProjectHost(services, Workspace, Dispatcher, _projectConfigurationFilePathStore, _projectManager);

        await Task.Run(async () => await host.LoadAsync());
        Assert.Empty(_projectManager.GetProjects());

        // Act
        await Task.Run(async () => await host.OnProjectChangedAsync(string.Empty, services.CreateUpdate(changes)));

        // Assert
        Assert.Empty(_projectManager.GetProjects());

        await Task.Run(async () => await host.DisposeAsync());
        Assert.Empty(_projectManager.GetProjects());
    }

    private class TestSuperFallbackWindowsRazorProjectHost : SuperFallbackWindowsRazorProjectHost
    {
        internal TestSuperFallbackWindowsRazorProjectHost(
            IUnconfiguredProjectCommonServices commonServices,
            Workspace workspace,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            ProjectConfigurationFilePathStore projectConfigurationFilePathStore,
            ProjectSnapshotManagerBase projectManager)
            : base(commonServices, workspace, projectSnapshotManagerDispatcher, projectConfigurationFilePathStore, projectManager)
        {
            base.SkipIntermediateOutputPathExistCheck_TestOnly = true;
        }

        protected override bool TryGetIntermediateOutputPath(IImmutableDictionary<string, IProjectRuleSnapshot> state, [NotNullWhen(true)] out string path)
        {
            path = "obj";
            return true;
        }
    }

    private class TestProjectSnapshotManager : DefaultProjectSnapshotManager
    {
        public TestProjectSnapshotManager(Workspace workspace, ProjectSnapshotManagerDispatcher dispatcher)
            : base(Mock.Of<IErrorReporter>(MockBehavior.Strict), Array.Empty<IProjectSnapshotChangeTrigger>(), workspace, dispatcher)
        {
        }
    }
}
