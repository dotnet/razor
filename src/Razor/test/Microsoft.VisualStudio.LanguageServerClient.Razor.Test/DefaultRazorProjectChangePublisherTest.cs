// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.OperationProgress;
using Moq;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.Test
{
    public class TestServiceProvider : IServiceProvider
    {
        public TestServiceProvider()
        {
        }

        public object GetService(Type serviceType)
        {
            return new TestVsOperationProgressStatusService();
        }

        private class TestVsOperationProgressStatusService : IVsOperationProgressStatusService
        {

            public TestVsOperationProgressStatusService()
            {
            }

            public IVsOperationProgressStageStatus GetStageStatus(string operationProgressStageId)
            {
                throw new NotImplementedException();
            }

            public IVsOperationProgressStageStatusForSolutionLoad GetStageStatusForSolutionLoad(string operationProgressStageId)
            {
                return new TestVsOperationProgressStageStatusForSolutionLoad();
            }

            private class TestVsOperationProgressStageStatusForSolutionLoad : IVsOperationProgressStageStatusForSolutionLoad
            {
                public bool IsInProgress => false;

                public event PropertyChangedEventHandler PropertyChanged;

                public Task WaitForCompletionAsync()
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("name"));
                    throw new NotImplementedException();
                }
            }
        }
    }

    public class DefaultRazorProjectChangePublisherTest : LanguageServerTestBase
    {
        private readonly RazorLogger _razorLogger = Mock.Of<RazorLogger>(MockBehavior.Strict);

        public DefaultRazorProjectChangePublisherTest()
        {
            ProjectSnapshotManager = CreateProjectSnapshotManager(allowNotifyListeners: true);
        }

        private ProjectSnapshotManagerBase ProjectSnapshotManager { get; }

        private ProjectConfigurationFilePathStore ProjectConfigurationFilePathStore { get; } = new DefaultProjectConfigurationFilePathStore();

        [Fact]
        [WorkItem("https://github.com/dotnet/aspnetcore/issues/35945")]
        public async Task ProjectManager_Changed_Remove_Change_NoopsOnDelayedPublish()
        {
            // Arrange
            var serializationSuccessful = false;
            var tagHelpers = new TagHelperDescriptor[] {
                new DefaultTagHelperDescriptor(FileKinds.Component, "Namespace.FileNameOther", "Assembly", "FileName", "FileName document", "FileName hint",
                    caseSensitive: false,tagMatchingRules: null, attributeDescriptors: null,  allowedChildTags: null, metadata: null, diagnostics: null)
            };
            var initialProjectSnapshot = CreateProjectSnapshot("/path/to/project.csproj", new ProjectWorkspaceState(tagHelpers, CodeAnalysis.CSharp.LanguageVersion.Preview));
            var expectedProjectSnapshot = CreateProjectSnapshot("/path/to/project.csproj", new ProjectWorkspaceState(ImmutableArray<TagHelperDescriptor>.Empty, CodeAnalysis.CSharp.LanguageVersion.Preview));
            var expectedConfigurationFilePath = "/path/to/obj/bin/Debug/project.razor.json";
            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
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
            publisher.Initialize(ProjectSnapshotManager);
            ProjectConfigurationFilePathStore.Set(expectedProjectSnapshot.FilePath, expectedConfigurationFilePath);
            var documentRemovedArgs = ProjectChangeEventArgs.CreateTestInstance(initialProjectSnapshot, initialProjectSnapshot, documentFilePath: "/path/to/file.razor", ProjectChangeKind.DocumentRemoved);
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
            var hostProject = new HostProject("/path/to/project.csproj", RazorConfiguration.Default, rootNamespace: "TestRootNamespace");
            var hostDocument = new HostDocument("/path/to/file.razor", "file.razor");
            ProjectSnapshotManager.ProjectAdded(hostProject);
            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
                onSerializeToFile: (snapshot, configurationFilePath) => attemptedToSerialize = true)
            {
                EnqueueDelay = 10,
            };
            publisher.Initialize(ProjectSnapshotManager);

            // Act
            ProjectSnapshotManager.DocumentAdded(hostProject, hostDocument, new EmptyTextLoader(hostDocument.FilePath));

            // Assert
            Assert.Empty(publisher.DeferredPublishTasks);
            Assert.False(attemptedToSerialize);
        }

        [Fact]
        public void ProjectManager_Changed_DocumentOpened_UninitializedProject_NotActive_Noops()
        {
            // Arrange
            var attemptedToSerialize = false;
            var hostProject = new HostProject("/path/to/project.csproj", RazorConfiguration.Default, rootNamespace: "TestRootNamespace");
            var hostDocument = new HostDocument("/path/to/file.razor", "file.razor");
            ProjectSnapshotManager.ProjectAdded(hostProject);
            ProjectSnapshotManager.DocumentAdded(hostProject, hostDocument, new EmptyTextLoader(hostDocument.FilePath));
            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
                onSerializeToFile: (snapshot, configurationFilePath) => attemptedToSerialize = true)
            {
                EnqueueDelay = 10,
            };
            publisher.Initialize(ProjectSnapshotManager);

            // Act
            ProjectSnapshotManager.DocumentOpened(hostProject.FilePath, hostDocument.FilePath, SourceText.From(string.Empty));

            // Assert
            Assert.Empty(publisher.DeferredPublishTasks);
            Assert.False(attemptedToSerialize);
        }

        [Fact]
        public void ProjectManager_Changed_DocumentOpened_InitializedProject_NotActive_Publishes()
        {
            // Arrange
            var serializationSuccessful = false;
            var hostProject = new HostProject("/path/to/project.csproj", RazorConfiguration.Default, rootNamespace: "TestRootNamespace");
            var hostDocument = new HostDocument("/path/to/file.razor", "file.razor");
            ProjectSnapshotManager.ProjectAdded(hostProject);
            ProjectSnapshotManager.ProjectWorkspaceStateChanged(hostProject.FilePath, ProjectWorkspaceState.Default);
            ProjectSnapshotManager.DocumentAdded(hostProject, hostDocument, new EmptyTextLoader(hostDocument.FilePath));
            var projectSnapshot = ProjectSnapshotManager.Projects[0];
            var expectedConfigurationFilePath = "/path/to/obj/bin/Debug/project.razor.json";
            ProjectConfigurationFilePathStore.Set(projectSnapshot.FilePath, expectedConfigurationFilePath);
            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
                onSerializeToFile: (snapshot, configurationFilePath) =>
                {
                    Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                    serializationSuccessful = true;
                })
            {
                EnqueueDelay = 10,
            };
            publisher.Initialize(ProjectSnapshotManager);

            // Act
            ProjectSnapshotManager.DocumentOpened(hostProject.FilePath, hostDocument.FilePath, SourceText.From(string.Empty));

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
            var projectSnapshot = CreateProjectSnapshot("/path/to/project.csproj", new ProjectWorkspaceState(ImmutableArray<TagHelperDescriptor>.Empty, CodeAnalysis.CSharp.LanguageVersion.CSharp7_3));
            var expectedConfigurationFilePath = "/path/to/obj/bin/Debug/project.razor.json";
            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
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
            publisher.Initialize(ProjectSnapshotManager);
            ProjectConfigurationFilePathStore.Set(projectSnapshot.FilePath, expectedConfigurationFilePath);
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
            var projectSnapshot = CreateProjectSnapshot("/path/to/project.csproj", new ProjectWorkspaceState(ImmutableArray<TagHelperDescriptor>.Empty, CodeAnalysis.CSharp.LanguageVersion.Default));
            var changedProjectSnapshot = CreateProjectSnapshot("/path/to/project.csproj", new ProjectWorkspaceState(ImmutableArray<TagHelperDescriptor>.Empty, CodeAnalysis.CSharp.LanguageVersion.CSharp8));
            var expectedConfigurationFilePath = "/path/to/obj/bin/Debug/project.razor.json";
            var aboutToChange = false;
            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
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
            publisher.Initialize(ProjectSnapshotManager);
            ProjectConfigurationFilePathStore.Set(projectSnapshot.FilePath, expectedConfigurationFilePath);
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
            var projectSnapshot = CreateProjectSnapshot("/path/to/project.csproj");
            var expectedConfigurationFilePath = "/path/to/obj/bin/Debug/project.razor.json";
            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
                onSerializeToFile: (snapshot, configurationFilePath) => attemptedToSerialize = true)
            {
                EnqueueDelay = 10,
                _active = true,
            };
            publisher.Initialize(ProjectSnapshotManager);
            ProjectConfigurationFilePathStore.Set(projectSnapshot.FilePath, expectedConfigurationFilePath);
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
            var firstSnapshot = CreateProjectSnapshot("/path/to/project.csproj");
            var secondSnapshot = CreateProjectSnapshot("/path/to/project.csproj", new[] { "/path/to/file.cshtml" });
            var expectedConfigurationFilePath = "/path/to/obj/bin/Debug/project.razor.json";
            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
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
            publisher.Initialize(ProjectSnapshotManager);
            ProjectConfigurationFilePathStore.Set(firstSnapshot.FilePath, expectedConfigurationFilePath);

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
            var firstSnapshot = CreateProjectSnapshot("/path/to/project.csproj");
            var secondSnapshot = CreateProjectSnapshot("/path/to/project.csproj", new[] { "/path/to/file.cshtml" });
            var expectedConfigurationFilePath = "/path/to/obj/bin/Debug/project.razor.json";
            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
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
            publisher.Initialize(ProjectSnapshotManager);
            ProjectConfigurationFilePathStore.Set(secondSnapshot.FilePath, expectedConfigurationFilePath);

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
            var firstSnapshot = CreateProjectSnapshot("/path/to/project.csproj");
            var tagHelpers = new TagHelperDescriptor[] {
                new DefaultTagHelperDescriptor(FileKinds.Component, "Namespace.FileNameOther", "Assembly", "FileName", "FileName document", "FileName hint",
                    caseSensitive: false,tagMatchingRules: null, attributeDescriptors: null,  allowedChildTags: null, metadata: null, diagnostics: null)
            };
            var secondSnapshot = CreateProjectSnapshot("/path/to/project.csproj", new ProjectWorkspaceState(tagHelpers, CodeAnalysis.CSharp.LanguageVersion.CSharp8), new string[]{
                "FileName.razor"
            });
            var expectedConfigurationFilePath = "/path/to/obj/bin/Debug/project.razor.json";
            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
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
            publisher.Initialize(ProjectSnapshotManager);
            ProjectConfigurationFilePathStore.Set(firstSnapshot.FilePath, expectedConfigurationFilePath);

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
            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger)
            {
                _active = true,
            };
            publisher.Initialize(ProjectSnapshotManager);
            var omniSharpProjectSnapshot = CreateProjectSnapshot("/path/to/project.csproj");

            // Act & Assert
            publisher.Publish(omniSharpProjectSnapshot);
        }

        [Fact]
        public void Publish_PublishesToSetPublishFilePath()
        {
            // Arrange
            var serializationSuccessful = false;
            var omniSharpProjectSnapshot = CreateProjectSnapshot("/path/to/project.csproj");
            var expectedConfigurationFilePath = "/path/to/obj/bin/Debug/project.razor.json";
            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
                onSerializeToFile: (snapshot, configurationFilePath) =>
                {
                    Assert.Same(omniSharpProjectSnapshot, snapshot);
                    Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                    serializationSuccessful = true;
                })
            {
                _active = true,
            };
            publisher.Initialize(ProjectSnapshotManager);
            ProjectConfigurationFilePathStore.Set(omniSharpProjectSnapshot.FilePath, expectedConfigurationFilePath);

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
            var expectedConfigurationFilePath = "/path/to/obj/bin/Debug/project.razor.json";

            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
                onSerializeToFile: (snapshot, configurationFilePath) =>
                {
                    Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                    serializationSuccessful = true;
                })
            {
                _active = true,
            };
            publisher.Initialize(ProjectSnapshotManager);
            var projectFilePath = "/path/to/project.csproj";
            var hostProject = new HostProject(projectFilePath, RazorConfiguration.Default, "TestRootNamespace");
            ProjectConfigurationFilePathStore.Set(hostProject.FilePath, expectedConfigurationFilePath);
            var projectWorkspaceState = new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), CodeAnalysis.CSharp.LanguageVersion.Default);

            // Act
            await RunOnDispatcherThreadAsync(() =>
            {
                ProjectSnapshotManager.ProjectAdded(hostProject);
                ProjectSnapshotManager.ProjectWorkspaceStateChanged(projectFilePath, projectWorkspaceState);
            }).ConfigureAwait(false);

            // Assert
            Assert.True(serializationSuccessful);
        }

        [UIFact]
        public async Task ProjectAdded_DoesNotPublishWithoutProjectWorkspaceStateAsync()
        {
            // Arrange
            var serializationSuccessful = false;
            var expectedConfigurationFilePath = "/path/to/obj/bin/Debug/project.razor.json";

            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
                onSerializeToFile: (snapshot, configurationFilePath) =>
                {
                    Assert.True(false, "Serialization should not have been atempted because there is no ProjectWorkspaceState.");
                    serializationSuccessful = true;
                })
            {
                _active = true,
            };
            publisher.Initialize(ProjectSnapshotManager);
            var hostProject = new HostProject("/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
            ProjectConfigurationFilePathStore.Set(hostProject.FilePath, expectedConfigurationFilePath);

            // Act
            await RunOnDispatcherThreadAsync(() => ProjectSnapshotManager.ProjectAdded(hostProject)).ConfigureAwait(false);

            Assert.Empty(publisher.DeferredPublishTasks);

            // Assert
            Assert.False(serializationSuccessful);
        }

        [UIFact]
        public async Task ProjectRemoved_UnSetPublishFilePath_NoopsAsync()
        {
            // Arrange
            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger)
            {
                _active = true,
            };
            publisher.Initialize(ProjectSnapshotManager);
            var hostProject = new HostProject("/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
            await RunOnDispatcherThreadAsync(() => ProjectSnapshotManager.ProjectAdded(hostProject)).ConfigureAwait(false);

            // Act & Assert
            await RunOnDispatcherThreadAsync(() => ProjectSnapshotManager.ProjectRemoved(hostProject)).ConfigureAwait(false);

            Assert.Empty(publisher.DeferredPublishTasks);
        }

        [UIFact]
        public async Task ProjectAdded_DoesNotFireWhenNotReadyAsync()
        {
            // Arrange
            var serializationSuccessful = false;
            var expectedConfigurationFilePath = "/path/to/obj/bin/Debug/project.razor.json";

            var publisher = new TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore,
                _razorLogger,
                onSerializeToFile: (snapshot, configurationFilePath) =>
                {
                    Assert.Equal(expectedConfigurationFilePath, configurationFilePath);
                    serializationSuccessful = true;
                },
                shouldSerialize: false)
            {
                _active = true,
            };
            publisher.Initialize(ProjectSnapshotManager);
            var projectFilePath = "/path/to/project.csproj";
            var hostProject = new HostProject(projectFilePath, RazorConfiguration.Default, "TestRootNamespace");
            ProjectConfigurationFilePathStore.Set(hostProject.FilePath, expectedConfigurationFilePath);
            var projectWorkspaceState = new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), CodeAnalysis.CSharp.LanguageVersion.Default);

            // Act
            await RunOnDispatcherThreadAsync(() =>
            {
                ProjectSnapshotManager.ProjectAdded(hostProject);
                ProjectSnapshotManager.ProjectWorkspaceStateChanged(projectFilePath, projectWorkspaceState);
            }).ConfigureAwait(false);

            // Assert
            Assert.False(serializationSuccessful);
        }

        internal static ProjectSnapshot CreateProjectSnapshot(string projectFilePath, ProjectWorkspaceState projectWorkspaceState = null, string[] documentFilePaths = null)
        {
            if (documentFilePaths is null)
            {
                documentFilePaths = Array.Empty<string>();
            }

            var testProjectSnapshot = TestProjectSnapshot.Create(projectFilePath, documentFilePaths, projectWorkspaceState);

            return testProjectSnapshot;
        }

        internal static ProjectSnapshot CreateProjectSnapshot(string projectFilePath, string[] documentFilePaths)
        {
            var testProjectSnapshot = TestProjectSnapshot.Create(projectFilePath, documentFilePaths);

            return testProjectSnapshot;
        }

        internal ProjectSnapshotManagerBase CreateProjectSnapshotManager(bool allowNotifyListeners = false)
        {
            var snapshotManager = TestProjectSnapshotManager.Create(Dispatcher);
            snapshotManager.AllowNotifyListeners = allowNotifyListeners;

            return snapshotManager;
        }

        protected Task RunOnDispatcherThreadAsync(Action action)
        {
            return Dispatcher.RunOnDispatcherThreadAsync(
                () => action(),
                CancellationToken.None);
        }

        protected Task<TReturn> RunOnDispatcherThreadAsync<TReturn>(Func<TReturn> action)
        {
            return Dispatcher.RunOnDispatcherThreadAsync(
                () => action(),
                CancellationToken.None);
        }

        protected Task RunOnDispatcherThreadAsync(Func<Task> action)
        {
            return Dispatcher.RunOnDispatcherThreadAsync(
                async () => await action().ConfigureAwait(true),
                CancellationToken.None);
        }

        private class TestDefaultRazorProjectChangePublisher : DefaultRazorProjectChangePublisher
        {
            private static readonly Mock<LSPEditorFeatureDetector> s_lspEditorFeatureDetector = new Mock<LSPEditorFeatureDetector>(MockBehavior.Strict);

            private readonly Action<ProjectSnapshot, string> _onSerializeToFile;

            private readonly bool _shouldSerialize;
            private readonly bool _useRealShouldSerialize;

            static TestDefaultRazorProjectChangePublisher()
            {
                s_lspEditorFeatureDetector
                    .Setup(t => t.IsLSPEditorAvailable())
                    .Returns(true);
            }

            public TestDefaultRazorProjectChangePublisher(
                ProjectConfigurationFilePathStore projectStatePublishFilePathStore,
                RazorLogger logger,
                Action<ProjectSnapshot, string> onSerializeToFile = null,
                bool shouldSerialize = true,
                bool useRealShouldSerialize = false)
                : base(s_lspEditorFeatureDetector.Object, projectStatePublishFilePathStore, new TestServiceProvider(), logger)
            {
                _onSerializeToFile = onSerializeToFile ?? ((_1, _2) => throw new XunitException("SerializeToFile should not have been called."));
                _shouldSerialize = shouldSerialize;
                _useRealShouldSerialize = useRealShouldSerialize;
            }

            protected override bool FileExists(string file)
            {
                return true;
            }

            protected override void SerializeToFile(ProjectSnapshot projectSnapshot, string configurationFilePath) => _onSerializeToFile?.Invoke(projectSnapshot, configurationFilePath);

            protected override bool ShouldSerialize(ProjectSnapshot projectSnapshot, string configurationFilePath)
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
}
