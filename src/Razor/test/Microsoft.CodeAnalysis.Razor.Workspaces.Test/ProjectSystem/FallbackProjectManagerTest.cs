// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Test.ProjectSystem;

public class FallbackProjectManagerTest : WorkspaceTestBase
{
    private FallbackProjectManager _fallbackProjectManger;
    private TestProjectSnapshotManager _projectSnapshotManager;
    private TestProjectConfigurationFilePathStore _projectConfigurationFilePathStore;

    public FallbackProjectManagerTest(ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
        var languageServerFeatureOptions = TestLanguageServerFeatureOptions.Instance;
        _projectConfigurationFilePathStore = new TestProjectConfigurationFilePathStore();

        var dispatcher = Mock.Of<ProjectSnapshotManagerDispatcher>(MockBehavior.Strict);
        _projectSnapshotManager = new TestProjectSnapshotManager(Workspace, dispatcher);

        var projectSnapshotManagerAccessor = Mock.Of<ProjectSnapshotManagerAccessor>(a => a.Instance == _projectSnapshotManager, MockBehavior.Strict);

        _fallbackProjectManger = new FallbackProjectManager(_projectConfigurationFilePathStore, languageServerFeatureOptions, projectSnapshotManagerAccessor);
    }

    [Fact]
    public void DynamicFileAdded_KnownProject_DoesNothing()
    {
        var projectFilePath = @"C:\path\to\project.csproj";
        var hostProject = new HostProject(projectFilePath, @"C:\path\to\obj", RazorConfiguration.Default, "RootNamespace", "DisplayName");
        _projectSnapshotManager.ProjectAdded(hostProject);

        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Default, "DisplayName", "AssemblyName", LanguageNames.CSharp, filePath: projectFilePath)
            .WithCompilationOutputInfo(new CompilationOutputInfo().WithAssemblyPath(@"C:\path\to\obj\project.dll"));
        Workspace.TryApplyChanges(Workspace.CurrentSolution.AddProject(projectInfo));

        _fallbackProjectManger.DynamicFileAdded(projectId, hostProject.Key, projectFilePath, @"C:\path\to\file.razor");

        Assert.Empty(_fallbackProjectManger.GetTestAccessor().ProjectIds);
    }

    [Fact]
    public void DynamicFileAdded_UnknownProject_Adds()
    {
        var projectFilePath = @"C:\path\to\project.csproj";
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Default, "DisplayName", "AssemblyName", LanguageNames.CSharp, filePath: projectFilePath)
            .WithCompilationOutputInfo(new CompilationOutputInfo().WithAssemblyPath(@"C:\path\to\obj\project.dll"))
            .WithDefaultNamespace("RootNamespace");

        Workspace.TryApplyChanges(Workspace.CurrentSolution.AddProject(projectInfo));

        var projectKey = TestProjectKey.Create(@"C:\path\to\obj");

        _fallbackProjectManger.DynamicFileAdded(projectId, projectKey, projectFilePath, @"C:\path\to\file.razor");

        var actualId = Assert.Single(_fallbackProjectManger.GetTestAccessor().ProjectIds);
        Assert.Equal(projectId, actualId);

        var project = Assert.Single(_projectSnapshotManager.GetProjects());
        Assert.Equal("DisplayName", project.DisplayName);
        Assert.Equal("RootNamespace", project.RootNamespace);

        var documentFilePath = Assert.Single(project.DocumentFilePaths);
        Assert.Equal(@"C:\path\to\file.razor", documentFilePath);
    }

    [Fact]
    public void DynamicFileAdded_TrackedProject_AddsDocuments()
    {
        var projectFilePath = @"C:\path\to\project.csproj";
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Default, "DisplayName", "AssemblyName", LanguageNames.CSharp, filePath: projectFilePath)
            .WithCompilationOutputInfo(new CompilationOutputInfo().WithAssemblyPath(@"C:\path\to\obj\project.dll"))
            .WithDefaultNamespace("RootNamespace");

        Workspace.TryApplyChanges(Workspace.CurrentSolution.AddProject(projectInfo));

        var projectKey = TestProjectKey.Create(@"C:\path\to\obj");

        _fallbackProjectManger.DynamicFileAdded(projectId, projectKey, projectFilePath, @"C:\path\to\file.razor");

        _fallbackProjectManger.DynamicFileAdded(projectId, projectKey, projectFilePath, @"C:\path\to\new_file.razor");

        _fallbackProjectManger.DynamicFileAdded(projectId, projectKey, projectFilePath, @"C:\path\to\file.cshtml");

        var project = Assert.Single(_projectSnapshotManager.GetProjects());

        Assert.Collection(project.DocumentFilePaths.OrderBy(f => f),
            f => Assert.Equal(@"C:\path\to\file.cshtml", f),
            f => Assert.Equal(@"C:\path\to\file.razor", f),
            f => Assert.Equal(@"C:\path\to\new_file.razor", f));
    }

    [Fact]
    public void DynamicFileAdded_UnknownProject_SetsConfigurationFileStore()
    {
        var projectFilePath = @"C:\path\to\project.csproj";
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Default, "DisplayName", "AssemblyName", LanguageNames.CSharp, filePath: projectFilePath)
            .WithCompilationOutputInfo(new CompilationOutputInfo().WithAssemblyPath(@"C:\path\to\obj\project.dll"))
            .WithDefaultNamespace("RootNamespace");

        Workspace.TryApplyChanges(Workspace.CurrentSolution.AddProject(projectInfo));

        var projectKey = TestProjectKey.Create(@"C:\path\to\obj");

        _fallbackProjectManger.DynamicFileAdded(projectId, projectKey, projectFilePath, @"C:\path\to\file.razor");

        var kvp = Assert.Single(_projectConfigurationFilePathStore.GetMappings());
        Assert.Equal(projectKey, kvp.Key);
        Assert.Equal(@"C:\path\to\obj\project.razor.bin", kvp.Value);
    }
}
