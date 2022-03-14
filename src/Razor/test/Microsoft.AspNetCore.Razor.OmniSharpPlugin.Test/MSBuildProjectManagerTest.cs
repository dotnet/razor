﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.OmniSharpPlugin;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Moq;
using OmniSharp.MSBuild.Logging;
using OmniSharp.MSBuild.Notification;
using Xunit;
using Xunit.Sdk;

namespace Microsoft.AspNetCore.Razor.OmnisharpPlugin
{
    public class MSBuildProjectManagerTest : OmniSharpTestBase
    {
        public MSBuildProjectManagerTest()
        {
            CustomConfiguration = RazorConfiguration.Create(
                RazorLanguageVersion.Experimental,
                "Custom",
                Enumerable.Empty<RazorExtension>());
        }

        public RazorConfiguration CustomConfiguration { get; }

        [Fact]
        public async Task SynchronizeDocuments_UpdatesDocumentKinds()
        {
            // Arrange
            var msbuildProjectManager = new MSBuildProjectManager(
                Enumerable.Empty<ProjectConfigurationProvider>(),
                CreateProjectInstanceEvaluator(),
                Mock.Of<ProjectChangePublisher>(MockBehavior.Strict),
                Dispatcher,
                LoggerFactory);
            var projectManager = CreateProjectSnapshotManager();
            msbuildProjectManager.Initialize(projectManager);
            var hostProject = new OmniSharpHostProject("/path/to/project.csproj", CustomConfiguration, "TestRootNamespace");
            var configuredHostDocuments = new[]
            {
                new OmniSharpHostDocument("file.cshtml", "file.cshtml", FileKinds.Component),
            };
            var projectSnapshot = await RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(hostProject);
                var hostDocument = new OmniSharpHostDocument("file.cshtml", "file.cshtml", FileKinds.Legacy);
                projectManager.DocumentAdded(hostProject, hostDocument);
                return projectManager.GetLoadedProject(hostProject.FilePath);
            }).ConfigureAwait(false);

            // Act
            await RunOnDispatcherThreadAsync(() =>
                msbuildProjectManager.SynchronizeDocuments(
                    configuredHostDocuments,
                    projectSnapshot,
                    hostProject)).ConfigureAwait(false);

            // Assert
            await RunOnDispatcherThreadAsync(() =>
            {
                var refreshedProject = projectManager.GetLoadedProject(hostProject.FilePath);
                var documentFilePath = Assert.Single(refreshedProject.DocumentFilePaths);
                var document = refreshedProject.GetDocument(documentFilePath);
                Assert.Equal("file.cshtml", document.FilePath);
                Assert.Equal("file.cshtml", document.TargetPath);
                Assert.Equal(FileKinds.Component, document.FileKind);
            }).ConfigureAwait(false);
        }

        [Fact]
        public async Task SynchronizeDocuments_RemovesTrackedDocuments()
        {
            // Arrange
            var msbuildProjectManager = new MSBuildProjectManager(
                Enumerable.Empty<ProjectConfigurationProvider>(),
                CreateProjectInstanceEvaluator(),
                Mock.Of<ProjectChangePublisher>(MockBehavior.Strict),
                Dispatcher,
                LoggerFactory);
            var projectManager = CreateProjectSnapshotManager();
            msbuildProjectManager.Initialize(projectManager);
            var hostProject = new OmniSharpHostProject("/path/to/project.csproj", CustomConfiguration, "TestRootNamespace");
            var projectSnapshot = await RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(hostProject);
                var hostDocument = new OmniSharpHostDocument("file.razor", "file.razor", FileKinds.Component);
                projectManager.DocumentAdded(hostProject, hostDocument);
                return projectManager.GetLoadedProject(hostProject.FilePath);
            }).ConfigureAwait(false);

            // Act
            await RunOnDispatcherThreadAsync(() =>
                msbuildProjectManager.SynchronizeDocuments(
                    configuredHostDocuments: Array.Empty<OmniSharpHostDocument>(),
                    projectSnapshot,
                    hostProject)).ConfigureAwait(false);

            // Assert
            await RunOnDispatcherThreadAsync(() =>
            {
                var refreshedProject = projectManager.GetLoadedProject(hostProject.FilePath);
                Assert.Empty(refreshedProject.DocumentFilePaths);
            }).ConfigureAwait(false);
        }

        [Fact]
        public async Task SynchronizeDocuments_IgnoresTrackedDocuments()
        {
            // Arrange
            var hostDocument = new OmniSharpHostDocument("file.razor", "file.razor", FileKinds.Component);
            var configuredHostDocuments = new[] { hostDocument };
            var msbuildProjectManager = new MSBuildProjectManager(
                Enumerable.Empty<ProjectConfigurationProvider>(),
                CreateProjectInstanceEvaluator(),
                Mock.Of<ProjectChangePublisher>(MockBehavior.Strict),
                Dispatcher,
                LoggerFactory);
            var projectManager = CreateProjectSnapshotManager(allowNotifyListeners: true);
            msbuildProjectManager.Initialize(projectManager);
            var hostProject = new OmniSharpHostProject("/path/to/project.csproj", CustomConfiguration, "TestRootNamespace");
            var projectSnapshot = await RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(hostProject);
                projectManager.DocumentAdded(hostProject, hostDocument);
                return projectManager.GetLoadedProject(hostProject.FilePath);
            }).ConfigureAwait(false);
            projectManager.Changed += (sender, args) => throw new XunitException("Should not have been notified");

            // Act & Assert
            await RunOnDispatcherThreadAsync(() =>
                msbuildProjectManager.SynchronizeDocuments(
                    configuredHostDocuments,
                    projectSnapshot,
                    hostProject)).ConfigureAwait(false);
        }

        [Fact]
        public async Task SynchronizeDocuments_AddsUntrackedDocuments()
        {
            // Arrange
            var configuredHostDocuments = new[] {
                new OmniSharpHostDocument("file.razor", "file.razor", FileKinds.Component),
            };
            var msbuildProjectManager = new MSBuildProjectManager(
                Enumerable.Empty<ProjectConfigurationProvider>(),
                CreateProjectInstanceEvaluator(),
                Mock.Of<ProjectChangePublisher>(MockBehavior.Strict),
                Dispatcher,
                LoggerFactory);
            var projectManager = CreateProjectSnapshotManager();
            msbuildProjectManager.Initialize(projectManager);
            var hostProject = new OmniSharpHostProject("/path/to/project.csproj", CustomConfiguration, "TestRootNamespace");
            var projectSnapshot = await RunOnDispatcherThreadAsync(() =>
            {
                projectManager.ProjectAdded(hostProject);
                return projectManager.GetLoadedProject(hostProject.FilePath);
            }).ConfigureAwait(false);

            // Act
            await RunOnDispatcherThreadAsync(() =>
                msbuildProjectManager.SynchronizeDocuments(
                    configuredHostDocuments,
                    projectSnapshot,
                    hostProject)).ConfigureAwait(false);

            // Assert
            await RunOnDispatcherThreadAsync(() =>
            {
                var refreshedProject = projectManager.GetLoadedProject(hostProject.FilePath);
                var document = refreshedProject.GetDocument("file.razor");
                Assert.Equal(FileKinds.Component, document.FileKind);
            }).ConfigureAwait(false);
        }

        // This is more of an integration level test to verify everything works end to end.
        [Fact]
        public async Task ProjectLoadedAsync_AddsNewProjectWithDocument()
        {
            // Arrange
            var projectRootElement = ProjectRootElement.Create("/project/project.csproj");
            var intermediateOutputPath = "/project/obj";
            projectRootElement.AddProperty(MSBuildProjectManager.IntermediateOutputPathPropertyName, intermediateOutputPath);
            var projectInstance = new ProjectInstance(projectRootElement);
            var hostDocument = new OmniSharpHostDocument("file.razor", "file.razor", FileKinds.Component);
            var projectConfiguration = new ProjectConfiguration(CustomConfiguration, new[] { hostDocument }, "TestRootNamespace");
            var configurationProvider = new Mock<ProjectConfigurationProvider>(MockBehavior.Strict);
            configurationProvider.Setup(provider => provider.TryResolveConfiguration(It.IsAny<ProjectConfigurationProviderContext>(), out projectConfiguration))
                .Returns(true);
            var projectChangePublisher = new Mock<ProjectChangePublisher>(MockBehavior.Strict);
            projectChangePublisher.Setup(p => p.SetPublishFilePath(It.IsAny<string>(), It.IsAny<string>())).Verifiable();
            var msbuildProjectManager = new MSBuildProjectManager(
                new[] { configurationProvider.Object },
                CreateProjectInstanceEvaluator(),
                projectChangePublisher.Object,
                Dispatcher,
                LoggerFactory);
            var projectManager = CreateProjectSnapshotManager();
            msbuildProjectManager.Initialize(projectManager);
            var args = new ProjectLoadedEventArgs(
                id: null,
                project: null,
                sessionId: Guid.NewGuid(),
                projectInstance,
                diagnostics: Enumerable.Empty<MSBuildDiagnostic>().ToImmutableArray(),
                isReload: false,
                projectIdIsDefinedInSolution: false,
                sourceFiles: Enumerable.Empty<string>().ToImmutableArray(),
                sdkVersion: default);

            // Act
            await msbuildProjectManager.ProjectLoadedAsync(args);

            // Assert
            var project = await RunOnDispatcherThreadAsync(() => Assert.Single(projectManager.Projects)).ConfigureAwait(false);
            Assert.Equal(projectInstance.ProjectFileLocation.File, project.FilePath);
            Assert.Same(CustomConfiguration, project.Configuration);
            var document = project.GetDocument(hostDocument.FilePath);
            Assert.NotNull(document);
        }

        [Fact]
        public void GetProjectConfiguration_ProvidersReturnsTrue_ReturnsConfig()
        {
            // Arrange
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            projectInstance.AddItem(MSBuildProjectManager.ProjectCapabilityItemType, CoreProjectConfigurationProvider.DotNetCoreRazorCapability);
            var provider1 = new Mock<ProjectConfigurationProvider>(MockBehavior.Strict);
            var configuration = new ProjectConfiguration(RazorConfiguration.Default, Array.Empty<OmniSharpHostDocument>(), "TestRootNamespace"); // Setting to non-null to ensure the listener doesn't return the config verbatim.
            provider1.Setup(p => p.TryResolveConfiguration(It.IsAny<ProjectConfigurationProviderContext>(), out configuration))
                .Returns(false);
            var provider2 = new Mock<ProjectConfigurationProvider>(MockBehavior.Strict);
            provider2.Setup(p => p.TryResolveConfiguration(It.IsAny<ProjectConfigurationProviderContext>(), out configuration))
                .Returns(true);

            // Act
            var result = MSBuildProjectManager.GetProjectConfiguration(projectInstance, new[] { provider1.Object, provider2.Object });

            // Assert
            Assert.Same(configuration, result);
        }

        [Fact]
        public void GetProjectConfiguration_SingleProviderReturnsFalse_ReturnsNull()
        {
            // Arrange
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            projectInstance.AddItem(MSBuildProjectManager.ProjectCapabilityItemType, CoreProjectConfigurationProvider.DotNetCoreRazorCapability);
            var provider = new Mock<ProjectConfigurationProvider>(MockBehavior.Strict);
            var configuration = new ProjectConfiguration(RazorConfiguration.Default, Array.Empty<OmniSharpHostDocument>(), "TestRootNamespace"); // Setting to non-null to ensure the listener doesn't return the config verbatim.
            provider.Setup(p => p.TryResolveConfiguration(It.IsAny<ProjectConfigurationProviderContext>(), out configuration))
                .Returns(false);

            // Act
            var result = MSBuildProjectManager.GetProjectConfiguration(projectInstance, Enumerable.Empty<ProjectConfigurationProvider>());

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetProjectConfiguration_NoProviders_ReturnsNull()
        {
            // Arrange
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());
            projectInstance.AddItem(MSBuildProjectManager.ProjectCapabilityItemType, CoreProjectConfigurationProvider.DotNetCoreRazorCapability);

            // Act
            var result = MSBuildProjectManager.GetProjectConfiguration(projectInstance, Enumerable.Empty<ProjectConfigurationProvider>());

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void TryResolveConfigurationOutputPath_MSBuildIntermediateOutputPath_Normalizes()
        {
            // Arrange
            var projectRootElement = ProjectRootElement.Create();

            // Note the ending \ here that gets normalized away.
            var intermediateOutputPath = "C:/project\\obj";
            projectRootElement.AddProperty(MSBuildProjectManager.IntermediateOutputPathPropertyName, intermediateOutputPath);
            var projectInstance = new ProjectInstance(projectRootElement);
            var expectedPath = string.Format(CultureInfo.InvariantCulture, "C:{0}project{0}obj{0}{1}", Path.DirectorySeparatorChar, LanguageServerConstants.DefaultProjectConfigurationFile);

            // Act
            var result = MSBuildProjectManager.TryResolveConfigurationOutputPath(projectInstance, out var path);

            // Assert
            Assert.True(result);
            Assert.Equal(expectedPath, path);
        }

        [Fact]
        public void TryResolveConfigurationOutputPath_NoIntermediateOutputPath_ReturnsFalse()
        {
            // Arrange
            var projectInstance = new ProjectInstance(ProjectRootElement.Create());

            // Act
            var result = MSBuildProjectManager.TryResolveConfigurationOutputPath(projectInstance, out var path);

            // Assert
            Assert.False(result);
            Assert.Null(path);
        }

        [Fact]
        public void TryResolveConfigurationOutputPath_RootedIntermediateOutputPath_ReturnsTrue()
        {
            // Arrange
            var projectRootElement = ProjectRootElement.Create();
            var intermediateOutputPath = string.Format(CultureInfo.InvariantCulture, "C:{0}project{0}obj", Path.DirectorySeparatorChar);
            projectRootElement.AddProperty(MSBuildProjectManager.IntermediateOutputPathPropertyName, intermediateOutputPath);
            var projectInstance = new ProjectInstance(projectRootElement);
            var expectedPath = Path.Combine(intermediateOutputPath, LanguageServerConstants.DefaultProjectConfigurationFile);

            // Act
            var result = MSBuildProjectManager.TryResolveConfigurationOutputPath(projectInstance, out var path);

            // Assert
            Assert.True(result);
            Assert.Equal(expectedPath, path);
        }

        [Fact]
        public void TryResolveConfigurationOutputPath_RelativeIntermediateOutputPath_ReturnsTrue()
        {
            // Arrange
            var projectRootElement = ProjectRootElement.Create();
            var intermediateOutputPath = "obj";
            projectRootElement.AddProperty(MSBuildProjectManager.IntermediateOutputPathPropertyName, intermediateOutputPath);

            // Project directory is automatically set to the current test project (it's a reserved MSBuild property).

            var projectInstance = new ProjectInstance(projectRootElement);

            // Act
            var result = MSBuildProjectManager.TryResolveConfigurationOutputPath(projectInstance, out var path);

            // Assert
            Assert.True(result);
            Assert.NotEmpty(path);
        }

        private static ProjectInstanceEvaluator CreateProjectInstanceEvaluator()
        {
            var projectInstanceEvaluator = new Mock<ProjectInstanceEvaluator>(MockBehavior.Strict);
            projectInstanceEvaluator.Setup(instance => instance.Evaluate(It.IsAny<ProjectInstance>()))
                .Returns<ProjectInstance>(pi => pi);
            return projectInstanceEvaluator.Object;
        }
    }
}
