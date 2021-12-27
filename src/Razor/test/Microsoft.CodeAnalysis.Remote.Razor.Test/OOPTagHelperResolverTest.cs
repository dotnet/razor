// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Remote.Razor.Test;
using Moq;
using Xunit;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    public class OOPTagHelperResolverTest : TagHelperDescriptorTestBase
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

        [Fact]
        public void CalculateTagHelpersFromDelta_NewProject()
        {
            // Arrange
            var resolver = new TestTagHelperResolver(EngineFactory, ErrorReporter, Workspace);
            var initialDelta = new TagHelperDeltaResult(Delta: false, ResultId: 1, Project1TagHelpers, Array.Empty<TagHelperDescriptor>());

            // Act
            var tagHelpers = resolver.PublicProduceTagHelpersFromDelta(Project1FilePath, lastResultId: -1, initialDelta);

            // Assert
            Assert.Equal(Project1TagHelpers, tagHelpers);
        }

        [Fact]
        public void CalculateTagHelpersFromDelta_DeltaFailedToApplyToKnownProject()
        {
            // Arrange
            var resolver = new TestTagHelperResolver(EngineFactory, ErrorReporter, Workspace);
            var initialDelta = new TagHelperDeltaResult(Delta: false, ResultId: 1, Project1TagHelpers, Array.Empty<TagHelperDescriptor>());
            resolver.PublicProduceTagHelpersFromDelta(Project1FilePath, lastResultId: -1, initialDelta);
            var newTagHelperSet = new[] { TagHelper1_Project1 };
            var failedDeltaApplication = new TagHelperDeltaResult(Delta: false, initialDelta.ResultId + 1, newTagHelperSet, Array.Empty<TagHelperDescriptor>());

            // Act
            var tagHelpers = resolver.PublicProduceTagHelpersFromDelta(Project1FilePath, initialDelta.ResultId, failedDeltaApplication);

            // Assert
            Assert.Equal(newTagHelperSet, tagHelpers);
        }

        [Fact]
        public void CalculateTagHelpersFromDelta_NoopResult()
        {
            // Arrange
            var resolver = new TestTagHelperResolver(EngineFactory, ErrorReporter, Workspace);
            var initialDelta = new TagHelperDeltaResult(Delta: false, ResultId: 1, Project1TagHelpers, Array.Empty<TagHelperDescriptor>());
            resolver.PublicProduceTagHelpersFromDelta(Project1FilePath, lastResultId: -1, initialDelta);
            var noopDelta = new TagHelperDeltaResult(Delta: true, initialDelta.ResultId, Array.Empty<TagHelperDescriptor>(), Array.Empty<TagHelperDescriptor>());

            // Act
            var tagHelpers = resolver.PublicProduceTagHelpersFromDelta(Project1FilePath, initialDelta.ResultId, noopDelta);

            // Assert
            Assert.Equal(Project1TagHelpers, tagHelpers);
        }

        [Fact]
        public void CalculateTagHelpersFromDelta_ReplacedTagHelpers()
        {
            // Arrange
            var resolver = new TestTagHelperResolver(EngineFactory, ErrorReporter, Workspace);
            var initialDelta = new TagHelperDeltaResult(Delta: false, ResultId: 1, Project1TagHelpers, Array.Empty<TagHelperDescriptor>());
            resolver.PublicProduceTagHelpersFromDelta(Project1FilePath, lastResultId: -1, initialDelta);
            var changedDelta = new TagHelperDeltaResult(Delta: true, initialDelta.ResultId + 1, new[] { TagHelper2_Project2 }, new[] { TagHelper2_Project1 });

            // Act
            var tagHelpers = resolver.PublicProduceTagHelpersFromDelta(Project1FilePath, initialDelta.ResultId, changedDelta);

            // Assert
            Assert.Equal(new[] { TagHelper1_Project1, TagHelper2_Project2 }, tagHelpers.OrderBy(th => th.Name));
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

            public IReadOnlyList<TagHelperDescriptor> PublicProduceTagHelpersFromDelta(string projectFilePath, int lastResultId, TagHelperDeltaResult deltaResult)
                => ProduceTagHelpersFromDelta(projectFilePath, lastResultId, deltaResult);

            protected override IReadOnlyList<TagHelperDescriptor> ProduceTagHelpersFromDelta(string projectFilePath, int lastResultId, TagHelperDeltaResult deltaResult)
                => base.ProduceTagHelpersFromDelta(projectFilePath, lastResultId, deltaResult);
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
