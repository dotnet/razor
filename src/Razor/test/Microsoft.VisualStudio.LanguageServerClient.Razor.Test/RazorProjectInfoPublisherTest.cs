﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Test;

public class RazorProjectInfoPublisherTest : LanguageServerTestBase
{
    private readonly ProjectSnapshotManagerBase _projectSnapshotManager;
    private readonly ProjectConfigurationFilePathStore _projectConfigurationFilePathStore;

    public RazorProjectInfoPublisherTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _projectSnapshotManager = CreateProjectSnapshotManager(allowNotifyListeners: true);
        _projectConfigurationFilePathStore = new DefaultProjectConfigurationFilePathStore();
    }

    [Fact]
    [WorkItem("https://github.com/dotnet/aspnetcore/issues/35945")]
    public async Task ProjectManager_Changed_Remove_Change_NoopsOnDelayedPublish()
    {
        // Arrange
        var serializationSuccessful = false;
        var tagHelpers = ImmutableArray.Create<TagHelperDescriptor>(
            new DefaultTagHelperDescriptor(FileKinds.Component, "Namespace.FileNameOther", "Assembly", "FileName", "FileName document", "FileName hint",
                caseSensitive: false, tagMatchingRules: null, attributeDescriptors: null, allowedChildTags: null, metadata: null, diagnostics: null));

        var initialProjectSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj", new ProjectWorkspaceState(tagHelpers, CodeAnalysis.CSharp.LanguageVersion.Preview));
        var expectedProjectSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj", new ProjectWorkspaceState(ImmutableArray<TagHelperDescriptor>.Empty, CodeAnalysis.CSharp.LanguageVersion.Preview));
        var expectedConfigurationFilePath = @"C:\path\to\obj\bin\Debug\project.razor.bin";
        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) =>
            {
                Assert.Same(expectedProjectSnapshot, snapshot);
                Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                serializationSuccessful = true;
            })
        {
            EnqueueDelay = 10,
            _active = true,
        };
        publisher.Initialize(_projectSnapshotManager);
        _projectConfigurationFilePathStore.Set(expectedProjectSnapshot.Key, expectedConfigurationFilePath);
        var documentRemovedArgs = ProjectChangeEventArgs.CreateTestInstance(initialProjectSnapshot, initialProjectSnapshot, documentFilePath: @"C:\path\to\file.razor", ProjectChangeKind.DocumentRemoved);
        var projectChangedArgs = ProjectChangeEventArgs.CreateTestInstance(initialProjectSnapshot, expectedProjectSnapshot, documentFilePath: null, ProjectChangeKind.ProjectChanged);

        // Act
        publisher.ProjectSnapshotManager_Changed(null, documentRemovedArgs);
        publisher.ProjectSnapshotManager_Changed(null, projectChangedArgs);

        // Assert
        var stalePublishTask = Assert.Single(publisher.DeferredPublishTasks);
        await stalePublishTask.Value.ConfigureAwait(false);
        Assert.True(serializationSuccessful);
    }

    [Fact]
    public void ProjectManager_Changed_NotActive_Noops()
    {
        // Arrange
        var attemptedToSerialize = false;
        var hostProject = new HostProject(@"C:\path\to\project.csproj", @"C:\path\to\obj", RazorConfiguration.Default, rootNamespace: "TestRootNamespace");
        var hostDocument = new HostDocument(@"C:\path\to\file.razor", "file.razor");
        _projectSnapshotManager.ProjectAdded(hostProject);
        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) => attemptedToSerialize = true)
        {
            EnqueueDelay = 10,
        };
        publisher.Initialize(_projectSnapshotManager);

        // Act
        _projectSnapshotManager.DocumentAdded(hostProject.Key, hostDocument, new EmptyTextLoader(hostDocument.FilePath));

        // Assert
        Assert.Empty(publisher.DeferredPublishTasks);
        Assert.False(attemptedToSerialize);
    }

    [Fact]
    public void ProjectManager_Changed_DocumentOpened_UninitializedProject_NotActive_Noops()
    {
        // Arrange
        var attemptedToSerialize = false;
        var hostProject = new HostProject(@"C:\path\to\project.csproj", @"C:\path\to\obj", RazorConfiguration.Default, rootNamespace: "TestRootNamespace");
        var hostDocument = new HostDocument(@"C:\path\to\file.razor", "file.razor");
        _projectSnapshotManager.ProjectAdded(hostProject);
        _projectSnapshotManager.DocumentAdded(hostProject.Key, hostDocument, new EmptyTextLoader(hostDocument.FilePath));
        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) => attemptedToSerialize = true)
        {
            EnqueueDelay = 10,
        };
        publisher.Initialize(_projectSnapshotManager);

        // Act
        _projectSnapshotManager.DocumentOpened(hostProject.Key, hostDocument.FilePath, SourceText.From(string.Empty));

        // Assert
        Assert.Empty(publisher.DeferredPublishTasks);
        Assert.False(attemptedToSerialize);
    }

    [Fact]
    public async Task ProjectManager_Changed_DocumentOpened_InitializedProject_NotActive_Publishes()
    {
        // Arrange
        var serializationSuccessful = false;
        var hostProject = new HostProject(@"C:\path\to\project.csproj", @"C:\path\to\obj", RazorConfiguration.Default, rootNamespace: "TestRootNamespace");
        var hostDocument = new HostDocument(@"C:\path\to\file.razor", "file.razor");
        _projectSnapshotManager.ProjectAdded(hostProject);
        _projectSnapshotManager.ProjectWorkspaceStateChanged(hostProject.Key, ProjectWorkspaceState.Default);
        _projectSnapshotManager.DocumentAdded(hostProject.Key, hostDocument, new EmptyTextLoader(hostDocument.FilePath));
        var projectSnapshot = _projectSnapshotManager.GetProjects()[0];
        var expectedConfigurationFilePath = @"C:\path\to\obj\bin\Debug\project.razor.bin";
        _projectConfigurationFilePathStore.Set(projectSnapshot.Key, expectedConfigurationFilePath);
        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) =>
            {
                Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                serializationSuccessful = true;
            })
        {
            EnqueueDelay = 10,
        };
        publisher.Initialize(_projectSnapshotManager);

        // Act
        await RunOnDispatcherThreadAsync(() =>
        {
            _projectSnapshotManager.DocumentOpened(hostProject.Key, hostDocument.FilePath, SourceText.From(string.Empty));
        }).ConfigureAwait(false);
        

        // Assert
        Assert.Empty(publisher.DeferredPublishTasks);
        Assert.True(serializationSuccessful);
    }

    [Theory]
    [InlineData(ProjectChangeKind.DocumentAdded)]
    [InlineData(ProjectChangeKind.DocumentRemoved)]
    [InlineData(ProjectChangeKind.ProjectChanged)]
    internal async Task ProjectManager_Changed_EnqueuesPublishAsync(ProjectChangeKind changeKind)
    {
        // Arrange
        var serializationSuccessful = false;
        var projectSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj", new ProjectWorkspaceState(ImmutableArray<TagHelperDescriptor>.Empty, CodeAnalysis.CSharp.LanguageVersion.CSharp7_3));
        var expectedConfigurationFilePath = @"C:\path\to\obj\bin\Debug\project.razor.bin";
        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) =>
            {
                Assert.Same(projectSnapshot, snapshot);
                Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                serializationSuccessful = true;
            })
        {
            EnqueueDelay = 10,
            _active = true,
        };
        publisher.Initialize(_projectSnapshotManager);
        _projectConfigurationFilePathStore.Set(projectSnapshot.Key, expectedConfigurationFilePath);
        var args = ProjectChangeEventArgs.CreateTestInstance(projectSnapshot, projectSnapshot, documentFilePath: null, changeKind);

        // Act
        publisher.ProjectSnapshotManager_Changed(null, args);

        // Assert
        var kvp = Assert.Single(publisher.DeferredPublishTasks);
        await kvp.Value.ConfigureAwait(false);
        Assert.True(serializationSuccessful);
    }

    [Fact]
    internal async Task ProjectManager_ChangedTagHelpers_PublishesImmediately()
    {
        // Arrange
        var serializationSuccessful = false;
        var projectSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj", new ProjectWorkspaceState(ImmutableArray<TagHelperDescriptor>.Empty, CodeAnalysis.CSharp.LanguageVersion.Default));
        var changedProjectSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj", new ProjectWorkspaceState(ImmutableArray<TagHelperDescriptor>.Empty, CodeAnalysis.CSharp.LanguageVersion.CSharp8));
        var expectedConfigurationFilePath = @"C:\path\to\obj\bin\Debug\project.razor.bin";
        var aboutToChange = false;
        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) =>
            {
                if (!aboutToChange)
                {
                    return;
                }

                Assert.Same(changedProjectSnapshot, snapshot);
                Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                serializationSuccessful = true;
            })
        {
            EnqueueDelay = 10,
            _active = true,
        };
        publisher.Initialize(_projectSnapshotManager);
        _projectConfigurationFilePathStore.Set(projectSnapshot.Key, expectedConfigurationFilePath);
        var args = ProjectChangeEventArgs.CreateTestInstance(projectSnapshot, projectSnapshot, documentFilePath: null, ProjectChangeKind.ProjectChanged);
        publisher.ProjectSnapshotManager_Changed(null, args);

        // Flush publish task
        var kvp = Assert.Single(publisher.DeferredPublishTasks);
        await kvp.Value.ConfigureAwait(false);
        aboutToChange = true;
        publisher.DeferredPublishTasks.Clear();

        var changedTagHelpersArgs = ProjectChangeEventArgs.CreateTestInstance(projectSnapshot, changedProjectSnapshot, documentFilePath: null, ProjectChangeKind.ProjectChanged);

        // Act
        publisher.ProjectSnapshotManager_Changed(null, changedTagHelpersArgs);

        // Assert
        Assert.Empty(publisher.DeferredPublishTasks);
        Assert.True(serializationSuccessful);
    }

    [Fact]
    public async Task ProjectManager_Changed_ProjectRemoved_AfterEnqueuedPublishAsync()
    {
        // Arrange
        var attemptedToSerialize = false;
        var projectSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj");
        var expectedConfigurationFilePath = @"C:\path\to\obj\bin\Debug\project.razor.bin";
        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) => attemptedToSerialize = true)
        {
            EnqueueDelay = 10,
            _active = true,
        };
        publisher.Initialize(_projectSnapshotManager);
        _projectConfigurationFilePathStore.Set(projectSnapshot.Key, expectedConfigurationFilePath);
        publisher.EnqueuePublish(projectSnapshot);
        var args = ProjectChangeEventArgs.CreateTestInstance(projectSnapshot, newer: null, documentFilePath: null, ProjectChangeKind.ProjectRemoved);

        // Act
        publisher.ProjectSnapshotManager_Changed(null, args);

        // Assert
        var kvp = Assert.Single(publisher.DeferredPublishTasks);
        await kvp.Value.ConfigureAwait(false);

        Assert.False(attemptedToSerialize);
    }

    [Fact]
    public async Task EnqueuePublish_BatchesPublishRequestsAsync()
    {
        // Arrange
        var serializationSuccessful = false;
        var firstSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj");
        var secondSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj", new[] { @"C:\path\to\file.cshtml" });
        var expectedConfigurationFilePath = @"C:\path\to\obj\bin\Debug\project.razor.bin";
        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) =>
            {
                Assert.Same(secondSnapshot, snapshot);
                Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                serializationSuccessful = true;
            })
        {
            EnqueueDelay = 10,
            _active = true,
        };
        publisher.Initialize(_projectSnapshotManager);
        _projectConfigurationFilePathStore.Set(firstSnapshot.Key, expectedConfigurationFilePath);

        // Act
        publisher.EnqueuePublish(firstSnapshot);
        publisher.EnqueuePublish(secondSnapshot);

        // Assert
        var kvp = Assert.Single(publisher.DeferredPublishTasks);
        await kvp.Value.ConfigureAwait(false);
        Assert.True(serializationSuccessful);
    }

    [Fact]
    public async Task EnqueuePublish_OnProjectWithoutRazor_Publishes()
    {
        // Arrange
        var serializationSuccessful = false;
        var firstSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj");
        var secondSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj", new[] { @"C:\path\to\file.cshtml" });
        var expectedConfigurationFilePath = @"C:\path\to\objbin\Debug\project.razor.bin";
        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) =>
            {
                Assert.Same(secondSnapshot, snapshot);
                Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                serializationSuccessful = true;
            },
            useRealShouldSerialize: true)
        {
            EnqueueDelay = 10,
            _active = true,
        };
        publisher.Initialize(_projectSnapshotManager);
        _projectConfigurationFilePathStore.Set(secondSnapshot.Key, expectedConfigurationFilePath);

        // Act
        publisher.EnqueuePublish(secondSnapshot);

        // Assert
        var kvp = Assert.Single(publisher.DeferredPublishTasks);
        await kvp.Value.ConfigureAwait(false);
        Assert.True(serializationSuccessful);
    }

    [Fact]
    public async Task EnqueuePublish_OnProjectBeforeTagHelperProcessed_DoesNotPublish()
    {
        // Arrange
        var serializationSuccessful = false;
        var firstSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj");
        var tagHelpers = ImmutableArray.Create<TagHelperDescriptor>(
            new DefaultTagHelperDescriptor(FileKinds.Component, "Namespace.FileNameOther", "Assembly", "FileName", "FileName document", "FileName hint",
                caseSensitive: false, tagMatchingRules: null, attributeDescriptors: null, allowedChildTags: null, metadata: null, diagnostics: null));

        var secondSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj", new ProjectWorkspaceState(tagHelpers, CodeAnalysis.CSharp.LanguageVersion.CSharp8), new string[]{
            "FileName.razor"
        });
        var expectedConfigurationFilePath = @"C:\path\to\obj\bin\Debug\project.razor.bin";
        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) =>
            {
                Assert.Same(secondSnapshot, snapshot);
                Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                serializationSuccessful = true;
            },
            useRealShouldSerialize: true)
        {
            EnqueueDelay = 10,
            _active = true,
        };
        publisher.Initialize(_projectSnapshotManager);
        _projectConfigurationFilePathStore.Set(firstSnapshot.Key, expectedConfigurationFilePath);

        // Act
        publisher.EnqueuePublish(secondSnapshot);

        // Assert
        var kvp = Assert.Single(publisher.DeferredPublishTasks);
        await kvp.Value.ConfigureAwait(false);
        Assert.False(serializationSuccessful);
    }

    [Fact]
    public void Publish_UnsetConfigurationFilePath_Noops()
    {
        // Arrange
        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore)
        {
            _active = true,
        };
        publisher.Initialize(_projectSnapshotManager);
        var omniSharpProjectSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj");

        // Act & Assert
        publisher.Publish(omniSharpProjectSnapshot);
    }

    [Fact]
    public void Publish_PublishesToSetPublishFilePath()
    {
        // Arrange
        var serializationSuccessful = false;
        var omniSharpProjectSnapshot = CreateProjectSnapshot(@"C:\path\to\project.csproj");
        var expectedConfigurationFilePath = @"C:\path\to\obj\bin\Debug\project.razor.bin";
        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) =>
            {
                Assert.Same(omniSharpProjectSnapshot, snapshot);
                Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                serializationSuccessful = true;
            })
        {
            _active = true,
        };
        publisher.Initialize(_projectSnapshotManager);
        _projectConfigurationFilePathStore.Set(omniSharpProjectSnapshot.Key, expectedConfigurationFilePath);

        // Act
        publisher.Publish(omniSharpProjectSnapshot);

        // Assert
        Assert.True(serializationSuccessful);
    }

    [UIFact]
    public async Task ProjectAdded_PublishesToCorrectFilePathAsync()
    {
        // Arrange
        var serializationSuccessful = false;
        var expectedConfigurationFilePath = @"C:\path\to\obj\bin\Debug\project.razor.bin";

        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) =>
            {
                Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                serializationSuccessful = true;
            })
        {
            _active = true,
        };
        publisher.Initialize(_projectSnapshotManager);
        var projectFilePath = @"C:\path\to\project.csproj";
        var hostProject = new HostProject(projectFilePath, Path.Combine(Path.GetDirectoryName(projectFilePath), "obj"), RazorConfiguration.Default, "TestRootNamespace");
        _projectConfigurationFilePathStore.Set(hostProject.Key, expectedConfigurationFilePath);
        var projectWorkspaceState = new ProjectWorkspaceState(ImmutableArray<TagHelperDescriptor>.Empty, CodeAnalysis.CSharp.LanguageVersion.Default);

        // Act
        await RunOnDispatcherThreadAsync(() =>
        {
            _projectSnapshotManager.ProjectAdded(hostProject);
            _projectSnapshotManager.ProjectWorkspaceStateChanged(hostProject.Key, projectWorkspaceState);
        }).ConfigureAwait(false);

        // Assert
        Assert.True(serializationSuccessful);
    }

    [UIFact]
    public async Task ProjectAdded_DoesNotPublishWithoutProjectWorkspaceStateAsync()
    {
        // Arrange
        var serializationSuccessful = false;
        var expectedConfigurationFilePath = @"C:\path\to\obj\bin\Debug\project.razor.bin";

        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) =>
            {
                Assert.True(false, "Serialization should not have been atempted because there is no ProjectWorkspaceState.");
                serializationSuccessful = true;
            })
        {
            _active = true,
        };
        publisher.Initialize(_projectSnapshotManager);
        var hostProject = new HostProject(@"C:\path\to\project.csproj", @"C:\path\to\obj", RazorConfiguration.Default, "TestRootNamespace");
        _projectConfigurationFilePathStore.Set(hostProject.Key, expectedConfigurationFilePath);

        // Act
        await RunOnDispatcherThreadAsync(() => _projectSnapshotManager.ProjectAdded(hostProject)).ConfigureAwait(false);

        Assert.Empty(publisher.DeferredPublishTasks);

        // Assert
        Assert.False(serializationSuccessful);
    }

    [UIFact]
    public async Task ProjectRemoved_UnSetPublishFilePath_NoopsAsync()
    {
        // Arrange
        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore)
        {
            _active = true,
        };
        publisher.Initialize(_projectSnapshotManager);
        var hostProject = new HostProject(@"C:\path\to\project.csproj", @"C:\path\to\obj", RazorConfiguration.Default, "TestRootNamespace");
        await RunOnDispatcherThreadAsync(() => _projectSnapshotManager.ProjectAdded(hostProject)).ConfigureAwait(false);

        // Act & Assert
        await RunOnDispatcherThreadAsync(() => _projectSnapshotManager.ProjectRemoved(hostProject.Key)).ConfigureAwait(false);

        Assert.Empty(publisher.DeferredPublishTasks);
    }

    [UIFact]
    public async Task ProjectAdded_DoesNotFireWhenNotReadyAsync()
    {
        // Arrange
        var serializationSuccessful = false;
        var expectedConfigurationFilePath = @"C:\path\to\obj\bin\Debug\project.razor.bin";

        var publisher = new TestRazorProjectInfoPublisher(
            _projectConfigurationFilePathStore,
            onSerializeToFile: (snapshot, configurationFilePath) =>
            {
                Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                serializationSuccessful = true;
            },
            shouldSerialize: false)
        {
            _active = true,
        };
        publisher.Initialize(_projectSnapshotManager);
        var projectFilePath = @"C:\path\to\project.csproj";
        var hostProject = new HostProject(projectFilePath, Path.Combine(Path.GetDirectoryName(projectFilePath), "obj"), RazorConfiguration.Default, "TestRootNamespace");
        _projectConfigurationFilePathStore.Set(hostProject.Key, expectedConfigurationFilePath);
        var projectWorkspaceState = new ProjectWorkspaceState(ImmutableArray<TagHelperDescriptor>.Empty, CodeAnalysis.CSharp.LanguageVersion.Default);

        // Act
        await RunOnDispatcherThreadAsync(() =>
        {
            _projectSnapshotManager.ProjectAdded(hostProject);
            _projectSnapshotManager.ProjectWorkspaceStateChanged(hostProject.Key, projectWorkspaceState);
        }).ConfigureAwait(false);

        // Assert
        Assert.False(serializationSuccessful);
    }

    internal static IProjectSnapshot CreateProjectSnapshot(string projectFilePath, ProjectWorkspaceState projectWorkspaceState = null, string[] documentFilePaths = null)
    {
        if (documentFilePaths is null)
        {
            documentFilePaths = Array.Empty<string>();
        }

        var testProjectSnapshot = TestProjectSnapshot.Create(projectFilePath, documentFilePaths, projectWorkspaceState);

        return testProjectSnapshot;
    }

    internal static IProjectSnapshot CreateProjectSnapshot(string projectFilePath, string[] documentFilePaths)
    {
        var testProjectSnapshot = TestProjectSnapshot.Create(projectFilePath, documentFilePaths);

        return testProjectSnapshot;
    }

    internal ProjectSnapshotManagerBase CreateProjectSnapshotManager(bool allowNotifyListeners = false)
    {
        var snapshotManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        snapshotManager.AllowNotifyListeners = allowNotifyListeners;

        return snapshotManager;
    }

    protected Task RunOnDispatcherThreadAsync(Action action)
        => Dispatcher.RunOnDispatcherThreadAsync(
            action,
            DisposalToken);

    protected Task<TReturn> RunOnDispatcherThreadAsync<TReturn>(Func<TReturn> func)
        => Dispatcher.RunOnDispatcherThreadAsync(
            func,
            DisposalToken);

    protected Task RunOnDispatcherThreadAsync(Func<Task> func)
        => Dispatcher.RunOnDispatcherThreadAsync(
            func,
            DisposalToken);

    private class TestRazorProjectInfoPublisher : RazorProjectInfoPublisher
    {
        private static readonly Mock<LSPEditorFeatureDetector> s_lspEditorFeatureDetector = new(MockBehavior.Strict);

        private readonly Action<IProjectSnapshot, string> _onSerializeToFile;

        private readonly bool _shouldSerialize;
        private readonly bool _useRealShouldSerialize;

        static TestRazorProjectInfoPublisher()
        {
            s_lspEditorFeatureDetector
                .Setup(t => t.IsLSPEditorAvailable())
                .Returns(true);
        }

        public TestRazorProjectInfoPublisher(
            ProjectConfigurationFilePathStore projectStatePublishFilePathStore,
            Action<IProjectSnapshot, string> onSerializeToFile = null,
            bool shouldSerialize = true,
            bool useRealShouldSerialize = false)
            : base(s_lspEditorFeatureDetector.Object, projectStatePublishFilePathStore, TestRazorLogger.Instance)
        {
            _onSerializeToFile = onSerializeToFile ?? ((_1, _2) => throw new XunitException("SerializeToFile should not have been called."));
            _shouldSerialize = shouldSerialize;
            _useRealShouldSerialize = useRealShouldSerialize;
        }

        protected override bool FileExists(string file)
        {
            return true;
        }

        protected override void SerializeToFile(IProjectSnapshot projectSnapshot, string configurationFilePath) => _onSerializeToFile?.Invoke(projectSnapshot, configurationFilePath);

        protected override bool ShouldSerialize(IProjectSnapshot projectSnapshot, string configurationFilePath)
        {
            if (_useRealShouldSerialize)
            {
                return base.ShouldSerialize(projectSnapshot, configurationFilePath);
            }
            else
            {
                return _shouldSerialize;
            }
        }
    }
}
