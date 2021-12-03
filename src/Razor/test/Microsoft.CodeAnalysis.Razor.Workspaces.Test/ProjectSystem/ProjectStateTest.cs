﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    public class ProjectStateTest : WorkspaceTestBase
    {
        public ProjectStateTest()
        {
            HostProject = new HostProject(TestProjectData.SomeProject.FilePath, FallbackRazorConfiguration.MVC_2_0, TestProjectData.SomeProject.RootNamespace);
            HostProjectWithConfigurationChange = new HostProject(TestProjectData.SomeProject.FilePath, FallbackRazorConfiguration.MVC_1_0, TestProjectData.SomeProject.RootNamespace);
            ProjectWorkspaceState = new ProjectWorkspaceState(new[]
            {
                TagHelperDescriptorBuilder.Create("TestTagHelper", "TestAssembly").Build(),
            }, default);

            SomeTagHelpers = new List<TagHelperDescriptor>
            {
                TagHelperDescriptorBuilder.Create("Test1", "TestAssembly").Build()
            };

            Documents = new HostDocument[]
            {
                TestProjectData.SomeProjectFile1,
                TestProjectData.SomeProjectFile2,

                // linked file
                TestProjectData.AnotherProjectNestedFile3,
            };

            Text = SourceText.From("Hello, world!");
            TextLoader = () => Task.FromResult(TextAndVersion.Create(Text, VersionStamp.Create()));
        }

        private HostDocument[] Documents { get; }

        private HostProject HostProject { get; }

        private HostProject HostProjectWithConfigurationChange { get; }

        private ProjectWorkspaceState ProjectWorkspaceState { get; }

        private TestTagHelperResolver TagHelperResolver { get; set; }

        private List<TagHelperDescriptor> SomeTagHelpers { get; }

        private Func<Task<TextAndVersion>> TextLoader { get; }

        private SourceText Text { get; }

        protected override void ConfigureWorkspaceServices(List<IWorkspaceService> services)
        {
            TagHelperResolver = new TestTagHelperResolver();
            services.Add(TagHelperResolver);
        }

        protected override void ConfigureProjectEngine(RazorProjectEngineBuilder builder)
        {
            builder.SetImportFeature(new TestImportProjectFeature());
        }

        [Fact]
        public void GetImportDocumentTargetPaths_DoesNotIncludeCurrentImport()
        {
            // Arrange
            var state = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState);

            // Act
            var paths = state.GetImportDocumentTargetPaths(TestProjectData.SomeProjectComponentImportFile1);

            // Assert
            Assert.DoesNotContain(TestProjectData.SomeProjectComponentImportFile1.TargetPath, paths);
        }

        [Fact]
        public void ProjectState_ConstructedNew()
        {
            // Arrange

            // Act
            var state = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState);

            // Assert
            Assert.Empty(state.Documents);
            Assert.NotEqual(VersionStamp.Default, state.Version);
        }

        [Fact]
        public void ProjectState_AddHostDocument_ToEmpty()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState);

            // Act
            var state = original.WithAddedHostDocument(Documents[0], DocumentState.EmptyLoader);

            // Assert
            Assert.NotEqual(original.Version, state.Version);

            Assert.Collection(
                state.Documents.OrderBy(kvp => kvp.Key),
                d => Assert.Same(Documents[0], d.Value.HostDocument));
            Assert.NotEqual(original.DocumentCollectionVersion, state.DocumentCollectionVersion);
        }

        [Fact] // When we first add a document, we have no way to read the text, so it's empty.
        public async Task ProjectState_AddHostDocument_DocumentIsEmpty()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState);

            // Act
            var state = original.WithAddedHostDocument(Documents[0], DocumentState.EmptyLoader);

            // Assert
            var text = await state.Documents[Documents[0].FilePath].GetTextAsync();
            Assert.Equal(0, text.Length);
        }

        [Fact]
        public void ProjectState_AddHostDocument_ToProjectWithDocuments()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Act
            var state = original.WithAddedHostDocument(Documents[0], DocumentState.EmptyLoader);

            // Assert
            Assert.NotEqual(original.Version, state.Version);

            Assert.Collection(
                state.Documents.OrderBy(kvp => kvp.Key),
                d => Assert.Same(Documents[2], d.Value.HostDocument),
                d => Assert.Same(Documents[0], d.Value.HostDocument),
                d => Assert.Same(Documents[1], d.Value.HostDocument));
            Assert.NotEqual(original.DocumentCollectionVersion, state.DocumentCollectionVersion);
        }

        [Fact]
        public void ProjectState_AddHostDocument_TracksImports()
        {
            // Arrange

            // Act
            var state = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(TestProjectData.SomeProjectFile1, DocumentState.EmptyLoader)
                .WithAddedHostDocument(TestProjectData.SomeProjectFile2, DocumentState.EmptyLoader)
                .WithAddedHostDocument(TestProjectData.SomeProjectNestedFile3, DocumentState.EmptyLoader)
                .WithAddedHostDocument(TestProjectData.AnotherProjectNestedFile4, DocumentState.EmptyLoader);

            // Assert
            Assert.Collection(
                state.ImportsToRelatedDocuments.OrderBy(kvp => kvp.Key),
                kvp =>
                {
                    Assert.Equal(TestProjectData.SomeProjectImportFile.TargetPath, kvp.Key);
                    Assert.Equal(
                        new string[]
                        {
                            TestProjectData.AnotherProjectNestedFile4.FilePath,
                            TestProjectData.SomeProjectFile1.FilePath,
                            TestProjectData.SomeProjectFile2.FilePath,
                            TestProjectData.SomeProjectNestedFile3.FilePath,
                        },
                        kvp.Value.OrderBy(f => f));
                },
                kvp =>
                {
                    Assert.Equal(TestProjectData.SomeProjectNestedImportFile.TargetPath, kvp.Key);
                    Assert.Equal(
                        new string[]
                        {
                            TestProjectData.AnotherProjectNestedFile4.FilePath,
                            TestProjectData.SomeProjectNestedFile3.FilePath,
                        },
                        kvp.Value.OrderBy(f => f));
                });
        }

        [Fact]
        public void ProjectState_AddHostDocument_TracksImports_AddImportFile()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(TestProjectData.SomeProjectFile1, DocumentState.EmptyLoader)
                .WithAddedHostDocument(TestProjectData.SomeProjectFile2, DocumentState.EmptyLoader)
                .WithAddedHostDocument(TestProjectData.SomeProjectNestedFile3, DocumentState.EmptyLoader)
                .WithAddedHostDocument(TestProjectData.AnotherProjectNestedFile4, DocumentState.EmptyLoader);

            // Act
            var state = original
                .WithAddedHostDocument(TestProjectData.AnotherProjectImportFile, DocumentState.EmptyLoader);

            // Assert
            Assert.Collection(
                state.ImportsToRelatedDocuments.OrderBy(kvp => kvp.Key),
                kvp =>
                {
                    Assert.Equal(TestProjectData.SomeProjectImportFile.TargetPath, kvp.Key);
                    Assert.Equal(
                        new string[]
                        {
                            TestProjectData.AnotherProjectNestedFile4.FilePath,
                            TestProjectData.SomeProjectFile1.FilePath,
                            TestProjectData.SomeProjectFile2.FilePath,
                            TestProjectData.SomeProjectNestedFile3.FilePath,
                        },
                        kvp.Value.OrderBy(f => f));
                },
                kvp =>
                {
                    Assert.Equal(TestProjectData.SomeProjectNestedImportFile.TargetPath, kvp.Key);
                    Assert.Equal(
                        new string[]
                        {
                            TestProjectData.AnotherProjectNestedFile4.FilePath,
                            TestProjectData.SomeProjectNestedFile3.FilePath,
                        },
                        kvp.Value.OrderBy(f => f));
                });
        }

        [Fact]
        public void ProjectState_AddHostDocument_RetainsComputedState()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Force init
            var originalTagHelpers = original.TagHelpers;
            var originalProjectWorkspaceStateVersion = original.ProjectWorkspaceStateVersion;

            // Act
            var state = original.WithAddedHostDocument(Documents[0], DocumentState.EmptyLoader);

            // Assert
            var actualTagHelpers = state.TagHelpers;
            var actualProjectWorkspaceStateVersion = state.ProjectWorkspaceStateVersion;

            Assert.Same(original.ProjectEngine, state.ProjectEngine);
            Assert.Same(originalTagHelpers, actualTagHelpers);
            Assert.Equal(originalProjectWorkspaceStateVersion, actualProjectWorkspaceStateVersion);

            Assert.Same(original.Documents[Documents[1].FilePath], state.Documents[Documents[1].FilePath]);
            Assert.Same(original.Documents[Documents[2].FilePath], state.Documents[Documents[2].FilePath]);
        }

        [Fact]
        public void ProjectState_AddHostDocument_DuplicateNoops()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Act
            var state = original.WithAddedHostDocument(new HostDocument(Documents[1].FilePath, "SomePath.cshtml"), DocumentState.EmptyLoader);

            // Assert
            Assert.Same(original, state);
        }

        [Fact]
        public async Task ProjectState_WithChangedHostDocument_Loader()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Act
            var state = original.WithChangedHostDocument(Documents[1], TextLoader);

            // Assert
            Assert.NotEqual(original.Version, state.Version);

            var text = await state.Documents[Documents[1].FilePath].GetTextAsync();
            Assert.Same(Text, text);

            Assert.Equal(original.DocumentCollectionVersion, state.DocumentCollectionVersion);
        }

        [Fact]
        public async Task ProjectState_WithChangedHostDocument_Snapshot()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Act
            var state = original.WithChangedHostDocument(Documents[1], Text, VersionStamp.Create());

            // Assert
            Assert.NotEqual(original.Version, state.Version);

            var text = await state.Documents[Documents[1].FilePath].GetTextAsync();
            Assert.Same(Text, text);

            Assert.Equal(original.DocumentCollectionVersion, state.DocumentCollectionVersion);
        }

        [Fact]
        public void ProjectState_WithChangedHostDocument_Loader_RetainsComputedState()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Force init
            var originalTagHelpers = original.TagHelpers;
            var originalProjectWorkspaceStateVersion = original.ProjectWorkspaceStateVersion;

            // Act
            var state = original.WithChangedHostDocument(Documents[1], TextLoader);

            // Assert
            var actualTagHelpers = state.TagHelpers;
            var actualProjectWorkspaceStateVersion = state.ProjectWorkspaceStateVersion;

            Assert.Same(original.ProjectEngine, state.ProjectEngine);
            Assert.Same(originalTagHelpers, actualTagHelpers);
            Assert.Equal(originalProjectWorkspaceStateVersion, actualProjectWorkspaceStateVersion);

            Assert.NotSame(original.Documents[Documents[1].FilePath], state.Documents[Documents[1].FilePath]);
        }

        [Fact]
        public void ProjectState_WithChangedHostDocument_Snapshot_RetainsComputedState()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Force init
            var originalTagHelpers = original.TagHelpers;
            var originalProjectWorkspaceStateVersion = original.ProjectWorkspaceStateVersion;

            // Act
            var state = original.WithChangedHostDocument(Documents[1], Text, VersionStamp.Create());

            // Assert
            var actualTagHelpers = state.TagHelpers;
            var actualProjectWorkspaceStateVersion = state.ProjectWorkspaceStateVersion;

            Assert.Same(original.ProjectEngine, state.ProjectEngine);
            Assert.Same(originalTagHelpers, actualTagHelpers);
            Assert.Equal(originalProjectWorkspaceStateVersion, actualProjectWorkspaceStateVersion);

            Assert.NotSame(original.Documents[Documents[1].FilePath], state.Documents[Documents[1].FilePath]);
        }

        [Fact]
        public void ProjectState_WithChangedHostDocument_Loader_NotFoundNoops()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Act
            var state = original.WithChangedHostDocument(Documents[0], TextLoader);

            // Assert
            Assert.Same(original, state);
        }

        [Fact]
        public void ProjectState_WithChangedHostDocument_Snapshot_NotFoundNoops()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Act
            var state = original.WithChangedHostDocument(Documents[0], Text, VersionStamp.Create());

            // Assert
            Assert.Same(original, state);
        }

        [Fact]
        public void ProjectState_RemoveHostDocument_FromProjectWithDocuments()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Act
            var state = original.WithRemovedHostDocument(Documents[1]);

            // Assert
            Assert.NotEqual(original.Version, state.Version);

            Assert.Collection(
                state.Documents.OrderBy(kvp => kvp.Key),
                d => Assert.Same(Documents[2], d.Value.HostDocument));

            Assert.NotEqual(original.DocumentCollectionVersion, state.DocumentCollectionVersion);
        }

        [Fact]
        public void ProjectState_RemoveHostDocument_TracksImports()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(TestProjectData.SomeProjectFile1, DocumentState.EmptyLoader)
                .WithAddedHostDocument(TestProjectData.SomeProjectFile2, DocumentState.EmptyLoader)
                .WithAddedHostDocument(TestProjectData.SomeProjectNestedFile3, DocumentState.EmptyLoader)
                .WithAddedHostDocument(TestProjectData.AnotherProjectNestedFile4, DocumentState.EmptyLoader);

            // Act
            var state = original.WithRemovedHostDocument(TestProjectData.SomeProjectNestedFile3);

            // Assert
            Assert.Collection(
                state.ImportsToRelatedDocuments.OrderBy(kvp => kvp.Key),
                kvp =>
                {
                    Assert.Equal(TestProjectData.SomeProjectImportFile.TargetPath, kvp.Key);
                    Assert.Equal(
                        new string[]
                        {
                            TestProjectData.AnotherProjectNestedFile4.FilePath,
                            TestProjectData.SomeProjectFile1.FilePath,
                            TestProjectData.SomeProjectFile2.FilePath,
                        },
                        kvp.Value.OrderBy(f => f));
                },
                kvp =>
                {
                    Assert.Equal(TestProjectData.SomeProjectNestedImportFile.TargetPath, kvp.Key);
                    Assert.Equal(
                        new string[]
                        {
                            TestProjectData.AnotherProjectNestedFile4.FilePath,
                        },
                        kvp.Value.OrderBy(f => f));
                });
        }

        [Fact]
        public void ProjectState_RemoveHostDocument_TracksImports_RemoveAllDocuments()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(TestProjectData.SomeProjectFile1, DocumentState.EmptyLoader)
                .WithAddedHostDocument(TestProjectData.SomeProjectFile2, DocumentState.EmptyLoader)
                .WithAddedHostDocument(TestProjectData.SomeProjectNestedFile3, DocumentState.EmptyLoader)
                .WithAddedHostDocument(TestProjectData.AnotherProjectNestedFile4, DocumentState.EmptyLoader);

            // Act
            var state = original
                .WithRemovedHostDocument(TestProjectData.SomeProjectFile1)
                .WithRemovedHostDocument(TestProjectData.SomeProjectFile2)
                .WithRemovedHostDocument(TestProjectData.SomeProjectNestedFile3)
                .WithRemovedHostDocument(TestProjectData.AnotherProjectNestedFile4);

            // Assert
            Assert.Empty(state.Documents);
            Assert.Empty(state.ImportsToRelatedDocuments);
        }

        [Fact]
        public void ProjectState_RemoveHostDocument_RetainsComputedState()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Force init
            var originalTagHelpers = original.TagHelpers;
            var originalProjectWorkspaceStateVersion = original.ProjectWorkspaceStateVersion;

            // Act
            var state = original.WithRemovedHostDocument(Documents[2]);

            // Assert
            var actualTagHelpers = state.TagHelpers;
            var actualProjectWorkspaceStateVersion = state.ProjectWorkspaceStateVersion;

            Assert.Same(original.ProjectEngine, state.ProjectEngine);
            Assert.Same(originalTagHelpers, actualTagHelpers);
            Assert.Equal(originalProjectWorkspaceStateVersion, actualProjectWorkspaceStateVersion);

            Assert.Same(original.Documents[Documents[1].FilePath], state.Documents[Documents[1].FilePath]);
        }

        [Fact]
        public void ProjectState_RemoveHostDocument_NotFoundNoops()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Act
            var state = original.WithRemovedHostDocument(Documents[0]);

            // Assert
            Assert.Same(original, state);
        }

        [Fact]
        public void ProjectState_WithHostProject_ConfigurationChange_UpdatesConfigurationState()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Force init
            var originalTagHelpers = original.TagHelpers;
            var originalProjectWorkspaceStateVersion = original.ConfigurationVersion;

            TagHelperResolver.TagHelpers = SomeTagHelpers;

            // Act
            var state = original.WithHostProject(HostProjectWithConfigurationChange);

            // Assert
            Assert.NotEqual(original.Version, state.Version);
            Assert.Same(HostProjectWithConfigurationChange, state.HostProject);

            var actualTagHelpers = state.TagHelpers;
            var actualProjectWorkspaceStateVersion = state.ConfigurationVersion;

            Assert.NotSame(original.ProjectEngine, state.ProjectEngine);
            Assert.Same(originalTagHelpers, actualTagHelpers);
            Assert.NotEqual(originalProjectWorkspaceStateVersion, actualProjectWorkspaceStateVersion);

            Assert.NotSame(original.Documents[Documents[1].FilePath], state.Documents[Documents[1].FilePath]);
            Assert.NotSame(original.Documents[Documents[2].FilePath], state.Documents[Documents[2].FilePath]);

            Assert.NotEqual(original.DocumentCollectionVersion, state.DocumentCollectionVersion);
        }

        [Fact]
        public void ProjectState_WithHostProject_RootNamespaceChange_UpdatesConfigurationState()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);
            var hostProjectWithRootNamespaceChange = new HostProject(
                original.HostProject.FilePath,
                original.HostProject.Configuration,
                "ChangedRootNamespace");

            // Force init
            _ = original.TagHelpers;
            _ = original.ConfigurationVersion;

            TagHelperResolver.TagHelpers = SomeTagHelpers;

            // Act
            var state = original.WithHostProject(hostProjectWithRootNamespaceChange);

            // Assert
            Assert.NotSame(original, state);
        }

        [Fact]
        public void ProjectState_WithHostProject_NoConfigurationChange_Noops()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Force init
            _ = original.ProjectWorkspaceStateVersion;

            // Act
            var state = original.WithHostProject(HostProject);

            // Assert
            Assert.Same(original, state);
        }

        [Fact]
        public void ProjectState_WithHostProject_CallsConfigurationChangeOnDocumentState()
        {
            // Arrange
            var callCount = 0;

            var documents = ImmutableDictionary.CreateBuilder<string, DocumentState>(FilePathComparer.Instance);
            documents[Documents[1].FilePath] = TestDocumentState.Create(Workspace.Services, Documents[1], onConfigurationChange: () => callCount++);
            documents[Documents[2].FilePath] = TestDocumentState.Create(Workspace.Services, Documents[2], onConfigurationChange: () => callCount++);

            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState);
            original.Documents = documents.ToImmutable();

            // Act
            var state = original.WithHostProject(HostProjectWithConfigurationChange);

            // Assert
            Assert.NotEqual(original.Version, state.Version);
            Assert.Same(HostProjectWithConfigurationChange, state.HostProject);
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void ProjectState_WithHostProject_ResetsImportedDocuments()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState);
            original = original.WithAddedHostDocument(TestProjectData.SomeProjectFile1, DocumentState.EmptyLoader);

            // Act
            var state = original.WithHostProject(HostProjectWithConfigurationChange);

            // Assert
            var importMap = Assert.Single(state.ImportsToRelatedDocuments);
            var documentFilePath = Assert.Single(importMap.Value);
            Assert.Equal(TestProjectData.SomeProjectFile1.FilePath, documentFilePath);
        }

        [Fact]
        public void ProjectState_WithProjectWorkspaceState_Removed()
        {
            // Arrange
            var emptyProjectWorkspaceState = new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), default);
            var original = ProjectState.Create(Workspace.Services, HostProject, emptyProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Force init
            var originalTagHelpers = original.TagHelpers;
            var originalProjectWorkspaceStateVersion = original.ProjectWorkspaceStateVersion;

            // Act
            var state = original.WithProjectWorkspaceState(null);

            // Assert
            Assert.NotEqual(original.Version, state.Version);
            Assert.Null(state.ProjectWorkspaceState);

            var actualTagHelpers = state.TagHelpers;
            var actualProjectWorkspaceStateVersion = state.ProjectWorkspaceStateVersion;

            // The configuration didn't change, and the tag helpers didn't actually change
            Assert.Same(original.ProjectEngine, state.ProjectEngine);
            Assert.Same(originalTagHelpers, actualTagHelpers);
            Assert.NotEqual(originalProjectWorkspaceStateVersion, actualProjectWorkspaceStateVersion);

            Assert.NotSame(original.Documents[Documents[1].FilePath], state.Documents[Documents[1].FilePath]);
            Assert.NotSame(original.Documents[Documents[2].FilePath], state.Documents[Documents[2].FilePath]);
        }

        [Fact]
        public void ProjectState_WithProjectWorkspaceState_Added()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, null)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Force init
            var originalTagHelpers = original.TagHelpers;
            var originalProjectWorkspaceStateVersion = original.ProjectWorkspaceStateVersion;
            var newProjectWorkspaceState = ProjectWorkspaceState.Default;

            // Act
            var state = original.WithProjectWorkspaceState(newProjectWorkspaceState);

            // Assert
            Assert.NotEqual(original.Version, state.Version);
            Assert.Same(newProjectWorkspaceState, state.ProjectWorkspaceState);

            var actualTagHelpers = state.TagHelpers;
            var actualProjectWorkspaceStateVersion = state.ProjectWorkspaceStateVersion;

            // The configuration didn't change, and the tag helpers didn't actually change
            Assert.Same(original.ProjectEngine, state.ProjectEngine);
            Assert.Same(originalTagHelpers, actualTagHelpers);
            Assert.NotEqual(originalProjectWorkspaceStateVersion, actualProjectWorkspaceStateVersion);

            Assert.NotSame(original.Documents[Documents[1].FilePath], state.Documents[Documents[1].FilePath]);
            Assert.NotSame(original.Documents[Documents[1].FilePath], state.Documents[Documents[1].FilePath]);
        }

        [Fact]
        public void ProjectState_WithProjectWorkspaceState_Changed()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Force init
            var originalTagHelpers = original.TagHelpers;
            var originalProjectWorkspaceStateVersion = original.ProjectWorkspaceStateVersion;

            var changed = new ProjectWorkspaceState(ProjectWorkspaceState.TagHelpers, LanguageVersion.CSharp6);

            // Act
            var state = original.WithProjectWorkspaceState(changed);

            // Assert
            Assert.NotEqual(original.Version, state.Version);
            Assert.Same(changed, state.ProjectWorkspaceState);

            var actualTagHelpers = state.TagHelpers;
            var actualProjectWorkspaceStateVersion = state.ProjectWorkspaceStateVersion;

            // The C# language version changed, and the tag helpers didn't change
            Assert.NotSame(original.ProjectEngine, state.ProjectEngine);
            Assert.Same(originalTagHelpers, actualTagHelpers);
            Assert.NotEqual(originalProjectWorkspaceStateVersion, actualProjectWorkspaceStateVersion);

            Assert.NotSame(original.Documents[Documents[1].FilePath], state.Documents[Documents[1].FilePath]);
            Assert.NotSame(original.Documents[Documents[2].FilePath], state.Documents[Documents[2].FilePath]);
        }

        [Fact]
        public void ProjectState_WithProjectWorkspaceState_Changed_TagHelpersChanged()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Force init
            var originalTagHelpers = original.TagHelpers;
            var originalProjectWorkspaceStateVersion = original.ProjectWorkspaceStateVersion;

            var changed = new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), default);

            // Now create some tag helpers
            TagHelperResolver.TagHelpers = SomeTagHelpers;

            // Act
            var state = original.WithProjectWorkspaceState(changed);

            // Assert
            Assert.NotEqual(original.Version, state.Version);
            Assert.Same(changed, state.ProjectWorkspaceState);

            var actualTagHelpers = state.TagHelpers;
            var actualProjectWorkspaceStateVersion = state.ProjectWorkspaceStateVersion;

            // The configuration didn't change, but the tag helpers did
            Assert.Same(original.ProjectEngine, state.ProjectEngine);
            Assert.NotEqual(originalTagHelpers, actualTagHelpers);
            Assert.NotEqual(originalProjectWorkspaceStateVersion, actualProjectWorkspaceStateVersion);
            Assert.Equal(state.Version, actualProjectWorkspaceStateVersion);

            Assert.NotSame(original.Documents[Documents[1].FilePath], state.Documents[Documents[1].FilePath]);
            Assert.NotSame(original.Documents[Documents[2].FilePath], state.Documents[Documents[2].FilePath]);
        }

        [Fact]
        public void ProjectState_WithProjectWorkspaceState_IdenticalState_Caches()
        {
            // Arrange
            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState)
                .WithAddedHostDocument(Documents[2], DocumentState.EmptyLoader)
                .WithAddedHostDocument(Documents[1], DocumentState.EmptyLoader);

            // Force init
            _ = original.TagHelpers;
            _ = original.ProjectWorkspaceStateVersion;

            var changed = new ProjectWorkspaceState(original.TagHelpers, original.CSharpLanguageVersion);

            // Now create some tag helpers
            TagHelperResolver.TagHelpers = SomeTagHelpers;

            // Act
            var state = original.WithProjectWorkspaceState(changed);

            // Assert
            Assert.Same(original, state);
        }

        [Fact]
        public void ProjectState_WithProjectWorkspaceState_CallsWorkspaceProjectChangeOnDocumentState()
        {
            // Arrange
            var callCount = 0;

            var documents = ImmutableDictionary.CreateBuilder<string, DocumentState>(FilePathComparer.Instance);
            documents[Documents[1].FilePath] = TestDocumentState.Create(Workspace.Services, Documents[1], onProjectWorkspaceStateChange: () => callCount++);
            documents[Documents[2].FilePath] = TestDocumentState.Create(Workspace.Services, Documents[2], onProjectWorkspaceStateChange: () => callCount++);

            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState);
            original.Documents = documents.ToImmutable();

            var changed = new ProjectWorkspaceState(Array.Empty<TagHelperDescriptor>(), default);

            // Act
            var state = original.WithProjectWorkspaceState(changed);

            // Assert
            Assert.NotEqual(original.Version, state.Version);
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void ProjectState_WhenImportDocumentAdded_CallsImportsChanged()
        {
            // Arrange
            var callCount = 0;

            var document1 = TestProjectData.SomeProjectFile1;
            var document2 = TestProjectData.SomeProjectFile2;
            var document3 = TestProjectData.SomeProjectNestedFile3;
            var document4 = TestProjectData.AnotherProjectNestedFile4;

            var documents = ImmutableDictionary.CreateBuilder<string, DocumentState>(FilePathComparer.Instance);
            documents[document1.FilePath] = TestDocumentState.Create(Workspace.Services, document1, onImportsChange: () => callCount++);
            documents[document2.FilePath] = TestDocumentState.Create(Workspace.Services, document2, onImportsChange: () => callCount++);
            documents[document3.FilePath] = TestDocumentState.Create(Workspace.Services, document3, onImportsChange: () => callCount++);
            documents[document4.FilePath] = TestDocumentState.Create(Workspace.Services, document4, onImportsChange: () => callCount++);

            var importsToRelatedDocuments = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(FilePathComparer.Instance);
            importsToRelatedDocuments.Add(
                TestProjectData.SomeProjectImportFile.TargetPath,
                ImmutableArray.Create(
                    TestProjectData.SomeProjectFile1.FilePath,
                    TestProjectData.SomeProjectFile2.FilePath,
                    TestProjectData.SomeProjectNestedFile3.FilePath,
                    TestProjectData.AnotherProjectNestedFile4.FilePath));
            importsToRelatedDocuments.Add(
                TestProjectData.SomeProjectNestedImportFile.TargetPath,
                ImmutableArray.Create(
                    TestProjectData.SomeProjectNestedFile3.FilePath,
                    TestProjectData.AnotherProjectNestedFile4.FilePath));

            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState);
            original.Documents = documents.ToImmutable();
            original.ImportsToRelatedDocuments = importsToRelatedDocuments.ToImmutable();

            // Act
            var state = original.WithAddedHostDocument(TestProjectData.AnotherProjectImportFile, DocumentState.EmptyLoader);

            // Assert
            Assert.NotEqual(original.Version, state.Version);
            Assert.Equal(4, callCount);
        }

        [Fact]
        public void ProjectState_WhenImportDocumentAdded_CallsImportsChanged_Nested()
        {
            // Arrange
            var callCount = 0;

            var document1 = TestProjectData.SomeProjectFile1;
            var document2 = TestProjectData.SomeProjectFile2;
            var document3 = TestProjectData.SomeProjectNestedFile3;
            var document4 = TestProjectData.AnotherProjectNestedFile4;

            var documents = ImmutableDictionary.CreateBuilder<string, DocumentState>(FilePathComparer.Instance);
            documents[document1.FilePath] = TestDocumentState.Create(Workspace.Services, document1, onImportsChange: () => callCount++);
            documents[document2.FilePath] = TestDocumentState.Create(Workspace.Services, document2, onImportsChange: () => callCount++);
            documents[document3.FilePath] = TestDocumentState.Create(Workspace.Services, document3, onImportsChange: () => callCount++);
            documents[document4.FilePath] = TestDocumentState.Create(Workspace.Services, document4, onImportsChange: () => callCount++);

            var importsToRelatedDocuments = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(FilePathComparer.Instance);
            importsToRelatedDocuments.Add(
                TestProjectData.SomeProjectImportFile.TargetPath,
                ImmutableArray.Create(
                    TestProjectData.SomeProjectFile1.FilePath,
                    TestProjectData.SomeProjectFile2.FilePath,
                    TestProjectData.SomeProjectNestedFile3.FilePath,
                    TestProjectData.AnotherProjectNestedFile4.FilePath));
            importsToRelatedDocuments.Add(
                TestProjectData.SomeProjectNestedImportFile.TargetPath,
                ImmutableArray.Create(
                    TestProjectData.SomeProjectNestedFile3.FilePath,
                    TestProjectData.AnotherProjectNestedFile4.FilePath));

            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState);
            original.Documents = documents.ToImmutable();
            original.ImportsToRelatedDocuments = importsToRelatedDocuments.ToImmutable();

            // Act
            var state = original.WithAddedHostDocument(TestProjectData.AnotherProjectNestedImportFile, DocumentState.EmptyLoader);

            // Assert
            Assert.NotEqual(original.Version, state.Version);
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void ProjectState_WhenImportDocumentChangedTextLoader_CallsImportsChanged()
        {
            // Arrange
            var callCount = 0;

            var document1 = TestProjectData.SomeProjectFile1;
            var document2 = TestProjectData.SomeProjectFile2;
            var document3 = TestProjectData.SomeProjectNestedFile3;
            var document4 = TestProjectData.AnotherProjectNestedFile4;
            var document5 = TestProjectData.AnotherProjectNestedImportFile;

            var documents = ImmutableDictionary.CreateBuilder<string, DocumentState>(FilePathComparer.Instance);
            documents[document1.FilePath] = TestDocumentState.Create(Workspace.Services, document1, onImportsChange: () => callCount++);
            documents[document2.FilePath] = TestDocumentState.Create(Workspace.Services, document2, onImportsChange: () => callCount++);
            documents[document3.FilePath] = TestDocumentState.Create(Workspace.Services, document3, onImportsChange: () => callCount++);
            documents[document4.FilePath] = TestDocumentState.Create(Workspace.Services, document4, onImportsChange: () => callCount++);
            documents[document5.FilePath] = TestDocumentState.Create(Workspace.Services, document5, onImportsChange: () => callCount++);

            var importsToRelatedDocuments = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(FilePathComparer.Instance);
            importsToRelatedDocuments.Add(
                TestProjectData.SomeProjectImportFile.TargetPath,
                ImmutableArray.Create(
                    TestProjectData.SomeProjectFile1.FilePath,
                    TestProjectData.SomeProjectFile2.FilePath,
                    TestProjectData.SomeProjectNestedFile3.FilePath,
                    TestProjectData.AnotherProjectNestedFile4.FilePath,
                    TestProjectData.AnotherProjectNestedImportFile.FilePath));
            importsToRelatedDocuments.Add(
                TestProjectData.SomeProjectNestedImportFile.TargetPath,
                ImmutableArray.Create(
                    TestProjectData.SomeProjectNestedFile3.FilePath,
                    TestProjectData.AnotherProjectNestedFile4.FilePath));

            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState);
            original.Documents = documents.ToImmutable();
            original.ImportsToRelatedDocuments = importsToRelatedDocuments.ToImmutable();

            // Act
            var state = original.WithChangedHostDocument(document5, DocumentState.EmptyLoader);

            // Assert
            Assert.NotEqual(original.Version, state.Version);
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void ProjectState_WhenImportDocumentChangedSnapshot_CallsImportsChanged()
        {
            // Arrange
            var callCount = 0;

            var document1 = TestProjectData.SomeProjectFile1;
            var document2 = TestProjectData.SomeProjectFile2;
            var document3 = TestProjectData.SomeProjectNestedFile3;
            var document4 = TestProjectData.AnotherProjectNestedFile4;
            var document5 = TestProjectData.AnotherProjectNestedImportFile;

            var documents = ImmutableDictionary.CreateBuilder<string, DocumentState>(FilePathComparer.Instance);
            documents[document1.FilePath] = TestDocumentState.Create(Workspace.Services, document1, onImportsChange: () => callCount++);
            documents[document2.FilePath] = TestDocumentState.Create(Workspace.Services, document2, onImportsChange: () => callCount++);
            documents[document3.FilePath] = TestDocumentState.Create(Workspace.Services, document3, onImportsChange: () => callCount++);
            documents[document4.FilePath] = TestDocumentState.Create(Workspace.Services, document4, onImportsChange: () => callCount++);
            documents[document5.FilePath] = TestDocumentState.Create(Workspace.Services, document5, onImportsChange: () => callCount++);

            var importsToRelatedDocuments = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(FilePathComparer.Instance);
            importsToRelatedDocuments.Add(
                TestProjectData.SomeProjectImportFile.TargetPath,
                ImmutableArray.Create(
                    TestProjectData.SomeProjectFile1.FilePath,
                    TestProjectData.SomeProjectFile2.FilePath,
                    TestProjectData.SomeProjectNestedFile3.FilePath,
                    TestProjectData.AnotherProjectNestedFile4.FilePath,
                    TestProjectData.AnotherProjectNestedImportFile.FilePath));
            importsToRelatedDocuments.Add(
                TestProjectData.SomeProjectNestedImportFile.TargetPath,
                ImmutableArray.Create(
                    TestProjectData.SomeProjectNestedFile3.FilePath,
                    TestProjectData.AnotherProjectNestedFile4.FilePath));

            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState);
            original.Documents = documents.ToImmutable();
            original.ImportsToRelatedDocuments = importsToRelatedDocuments.ToImmutable();

            // Act
            var state = original.WithChangedHostDocument(document5, Text, VersionStamp.Create());

            // Assert
            Assert.NotEqual(original.Version, state.Version);
            Assert.Equal(2, callCount);
        }

        [Fact]
        public void ProjectState_WhenImportDocumentRemoved_CallsImportsChanged()
        {
            // Arrange
            var callCount = 0;

            var document1 = TestProjectData.SomeProjectFile1;
            var document2 = TestProjectData.SomeProjectFile2;
            var document3 = TestProjectData.SomeProjectNestedFile3;
            var document4 = TestProjectData.AnotherProjectNestedFile4;
            var document5 = TestProjectData.AnotherProjectNestedImportFile;

            var documents = ImmutableDictionary.CreateBuilder<string, DocumentState>(FilePathComparer.Instance);
            documents[document1.FilePath] = TestDocumentState.Create(Workspace.Services, document1, onImportsChange: () => callCount++);
            documents[document2.FilePath] = TestDocumentState.Create(Workspace.Services, document2, onImportsChange: () => callCount++);
            documents[document3.FilePath] = TestDocumentState.Create(Workspace.Services, document3, onImportsChange: () => callCount++);
            documents[document4.FilePath] = TestDocumentState.Create(Workspace.Services, document4, onImportsChange: () => callCount++);
            documents[document5.FilePath] = TestDocumentState.Create(Workspace.Services, document5, onImportsChange: () => callCount++);

            var importsToRelatedDocuments = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(FilePathComparer.Instance);
            importsToRelatedDocuments.Add(
                TestProjectData.SomeProjectImportFile.TargetPath,
                ImmutableArray.Create(
                    TestProjectData.SomeProjectFile1.FilePath,
                    TestProjectData.SomeProjectFile2.FilePath,
                    TestProjectData.SomeProjectNestedFile3.FilePath,
                    TestProjectData.AnotherProjectNestedFile4.FilePath,
                    TestProjectData.AnotherProjectNestedImportFile.FilePath));
            importsToRelatedDocuments.Add(
                TestProjectData.SomeProjectNestedImportFile.TargetPath,
                ImmutableArray.Create(
                    TestProjectData.SomeProjectNestedFile3.FilePath,
                    TestProjectData.AnotherProjectNestedFile4.FilePath));

            var original = ProjectState.Create(Workspace.Services, HostProject, ProjectWorkspaceState);
            original.Documents = documents.ToImmutable();
            original.ImportsToRelatedDocuments = importsToRelatedDocuments.ToImmutable();

            // Act
            var state = original.WithRemovedHostDocument(document5);

            // Assert
            Assert.NotEqual(original.Version, state.Version);
            Assert.Equal(2, callCount);
        }

        private class TestDocumentState : DocumentState
        {
            public static TestDocumentState Create(
                HostWorkspaceServices services,
                HostDocument hostDocument,
                Func<Task<TextAndVersion>> loader = null,
                Action onTextChange = null,
                Action onTextLoaderChange = null,
                Action onConfigurationChange = null,
                Action onImportsChange = null,
                Action onProjectWorkspaceStateChange = null)
            {
                return new TestDocumentState(
                    services,
                    hostDocument,
                    null,
                    null,
                    loader,
                    onTextChange,
                    onTextLoaderChange,
                    onConfigurationChange,
                    onImportsChange,
                    onProjectWorkspaceStateChange);
            }

            private readonly Action _onTextChange;
            private readonly Action _onTextLoaderChange;
            private readonly Action _onConfigurationChange;
            private readonly Action _onImportsChange;
            private readonly Action _onProjectWorkspaceStateChange;

            private TestDocumentState(
                HostWorkspaceServices services,
                HostDocument hostDocument,
                SourceText text,
                VersionStamp? version,
                Func<Task<TextAndVersion>> loader,
                Action onTextChange,
                Action onTextLoaderChange,
                Action onConfigurationChange,
                Action onImportsChange,
                Action onProjectWorkspaceStateChange)
                : base(services, hostDocument, text, version, loader)
            {
                _onTextChange = onTextChange;
                _onTextLoaderChange = onTextLoaderChange;
                _onConfigurationChange = onConfigurationChange;
                _onImportsChange = onImportsChange;
                _onProjectWorkspaceStateChange = onProjectWorkspaceStateChange;
            }

            public override DocumentState WithText(SourceText sourceText, VersionStamp version)
            {
                _onTextChange?.Invoke();
                return base.WithText(sourceText, version);
            }

            public override DocumentState WithTextLoader(Func<Task<TextAndVersion>> loader)
            {
                _onTextLoaderChange?.Invoke();
                return base.WithTextLoader(loader);
            }

            public override DocumentState WithConfigurationChange()
            {
                _onConfigurationChange?.Invoke();
                return base.WithConfigurationChange();
            }

            public override DocumentState WithImportsChange()
            {
                _onImportsChange?.Invoke();
                return base.WithImportsChange();
            }

            public override DocumentState WithProjectWorkspaceStateChange()
            {
                _onProjectWorkspaceStateChange?.Invoke();
                return base.WithProjectWorkspaceStateChange();
            }
        }
    }
}
