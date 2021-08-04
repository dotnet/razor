﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Moq;
using Xunit;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    public class OOPTagHelperResolverTest
    {
        public OOPTagHelperResolverTest()
        {
            HostProject_For_2_0 = new HostProject("Test.csproj", FallbackRazorConfiguration.MVC_2_0, rootNamespace: null);
            HostProject_For_NonSerializableConfiguration = new HostProject(
                "Test.csproj",
                new ProjectSystemRazorConfiguration(RazorLanguageVersion.Version_2_1, "Random-0.1", Array.Empty<RazorExtension>()), rootNamespace: null);

            CustomFactories = new Lazy<IProjectEngineFactory, ICustomProjectEngineFactoryMetadata>[]
            {
                new Lazy<IProjectEngineFactory, ICustomProjectEngineFactoryMetadata>(
                    () => Mock.Of<IProjectEngineFactory>(MockBehavior.Strict),
                    new ExportCustomProjectEngineFactoryAttribute("MVC-2.0") { SupportsSerialization = true, }),

                // We don't really use this factory, we just use it to ensure that the call is going to go out of process.
                new Lazy<IProjectEngineFactory, ICustomProjectEngineFactoryMetadata>(
                    () => Mock.Of<IProjectEngineFactory>(MockBehavior.Strict),
                    new ExportCustomProjectEngineFactoryAttribute("Test-2") { SupportsSerialization = false, }),
            };

            FallbackFactory = new FallbackProjectEngineFactory();

            Workspace = new AdhocWorkspace();

            var info = ProjectInfo.Create(ProjectId.CreateNewId("Test"), VersionStamp.Default, "Test", "Test", LanguageNames.CSharp, filePath: "Test.csproj");
            WorkspaceProject = Workspace.CurrentSolution.AddProject(info).GetProject(info.Id);

            ErrorReporter = new DefaultErrorReporter();
            ProjectManager = new TestProjectSnapshotManager(Workspace);
            EngineFactory = new DefaultProjectSnapshotProjectEngineFactory(FallbackFactory, CustomFactories);
        }

        private ErrorReporter ErrorReporter { get; }

        private ProjectSnapshotProjectEngineFactory EngineFactory { get; }

        private Lazy<IProjectEngineFactory, ICustomProjectEngineFactoryMetadata>[] CustomFactories { get; }

        private IFallbackProjectEngineFactory FallbackFactory { get; }

        private HostProject HostProject_For_2_0 { get; }

        private HostProject HostProject_For_NonSerializableConfiguration { get; }

        private ProjectSnapshotManagerBase ProjectManager { get; }

        private Project WorkspaceProject { get; }

        private Workspace Workspace { get; }

        [Fact]
        public async Task GetTagHelpersAsync_WithSerializableCustomFactory_GoesOutOfProcess()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject_For_2_0);

            var projectSnapshot = ProjectManager.GetLoadedProject("Test.csproj");

            var resolver = new TestTagHelperResolver(EngineFactory, ErrorReporter, Workspace)
            {
                OnResolveOutOfProcess = (f, p) =>
                {
                    Assert.Same(CustomFactories[0].Value, f);
                    Assert.Same(projectSnapshot, p);

                    return Task.FromResult(TagHelperResolutionResult.Empty);
                },
            };

            var result = await resolver.GetTagHelpersAsync(WorkspaceProject, projectSnapshot);

            // Assert
            Assert.Same(TagHelperResolutionResult.Empty, result);
        }

        [Fact]
        public async Task GetTagHelpersAsync_WithNonSerializableCustomFactory_StaysInProcess()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject_For_NonSerializableConfiguration);

            var projectSnapshot = ProjectManager.GetLoadedProject("Test.csproj");

            var resolver = new TestTagHelperResolver(EngineFactory, ErrorReporter, Workspace)
            {
                OnResolveInProcess = (p) =>
                {
                    Assert.Same(projectSnapshot, p);

                    return Task.FromResult(TagHelperResolutionResult.Empty);
                },
            };

            var result = await resolver.GetTagHelpersAsync(WorkspaceProject, projectSnapshot);

            // Assert
            Assert.Same(TagHelperResolutionResult.Empty, result);
        }

        [Fact]
        public async Task GetTagHelpersAsync_OperationCanceledException_DoesNotGetWrapped()
        {
            // Arrange
            ProjectManager.ProjectAdded(HostProject_For_2_0);

            var projectSnapshot = ProjectManager.GetLoadedProject("Test.csproj");

            var cancellationToken = new CancellationToken(canceled: true);
            var resolver = new TestTagHelperResolver(EngineFactory, ErrorReporter, Workspace)
            {
                OnResolveInProcess = (p) =>
                {
                    Assert.Same(projectSnapshot, p);

                    return Task.FromResult(TagHelperResolutionResult.Empty);
                },
                OnResolveOutOfProcess = (f, p) =>
                {
                    Assert.Same(projectSnapshot, p);

                    throw new OperationCanceledException();
                }
            };

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await resolver.GetTagHelpersAsync(WorkspaceProject, projectSnapshot, cancellationToken));
        }

        private class TestTagHelperResolver : OOPTagHelperResolver
        {
            public TestTagHelperResolver(ProjectSnapshotProjectEngineFactory factory, ErrorReporter errorReporter, Workspace workspace)
                : base(factory, errorReporter, workspace)
            {
            }

            public Func<IProjectEngineFactory, ProjectSnapshot, Task<TagHelperResolutionResult>> OnResolveOutOfProcess { get; set; }

            public Func<ProjectSnapshot, Task<TagHelperResolutionResult>> OnResolveInProcess { get; set; }

            protected override Task<TagHelperResolutionResult> ResolveTagHelpersOutOfProcessAsync(IProjectEngineFactory factory, Project workspaceProject, ProjectSnapshot projectSnapshot, CancellationToken cancellationToken)
            {
                Assert.NotNull(OnResolveOutOfProcess);
                return OnResolveOutOfProcess(factory, projectSnapshot);
            }

            protected override Task<TagHelperResolutionResult> ResolveTagHelpersInProcessAsync(Project project, ProjectSnapshot projectSnapshot, CancellationToken cancellationToken)
            {
                Assert.NotNull(OnResolveInProcess);
                return OnResolveInProcess(projectSnapshot);
            }
        }

        private class TestProjectSnapshotManager : DefaultProjectSnapshotManager
        {
            public TestProjectSnapshotManager(Workspace workspace)
                : base(
                      CreateProjectSnapshotManagerDispatcher(),
                      Mock.Of<ErrorReporter>(MockBehavior.Strict),
                      Enumerable.Empty<ProjectSnapshotChangeTrigger>(),
                      workspace)
            {
            }

            private static ProjectSnapshotManagerDispatcher CreateProjectSnapshotManagerDispatcher()
            {
                var dispatcher = new Mock<ProjectSnapshotManagerDispatcher>(MockBehavior.Strict);
                dispatcher.Setup(d => d.AssertDispatcherThread(It.IsAny<string>())).Verifiable();
                dispatcher.Setup(d => d.IsDispatcherThread).Returns(true);
                dispatcher.Setup(d => d.DispatcherScheduler).Returns(TaskScheduler.FromCurrentSynchronizationContext());
                return dispatcher.Object;
            }
        }
    }
}
