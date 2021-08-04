﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    public class DocumentStateTest : WorkspaceTestBase
    {
        public DocumentStateTest()
        {
            TagHelperResolver = new TestTagHelperResolver();

            HostProject = new HostProject(TestProjectData.SomeProject.FilePath, FallbackRazorConfiguration.MVC_2_0, TestProjectData.SomeProject.RootNamespace);
            HostProjectWithConfigurationChange = new HostProject(TestProjectData.SomeProject.FilePath, FallbackRazorConfiguration.MVC_1_0, TestProjectData.SomeProject.RootNamespace);
            ProjectWorkspaceState = new ProjectWorkspaceState(new[]
            {
                TagHelperDescriptorBuilder.Create("TestTagHelper", "TestAssembly").Build(),
            }, default);

            HostDocument = TestProjectData.SomeProjectFile1;

            Text = SourceText.From("Hello, world!");
            TextLoader = () => Task.FromResult(TextAndVersion.Create(Text, VersionStamp.Create()));
        }

        private HostDocument HostDocument { get; }

        private HostProject HostProject { get; }

        private HostProject HostProjectWithConfigurationChange { get; }

        private ProjectWorkspaceState ProjectWorkspaceState { get; }

        private TestTagHelperResolver TagHelperResolver { get; }

        private Func<Task<TextAndVersion>> TextLoader { get; }

        private SourceText Text { get; }

        protected override void ConfigureWorkspaceServices(List<IWorkspaceService> services)
        {
            services.Add(TagHelperResolver);
        }

        [Fact]
        public async Task DocumentState_CreatedNew_HasEmptyText()
        {
            // Arrange & Act
            var state = DocumentState.Create(Workspace.Services, HostDocument, DocumentState.EmptyLoader);

            // Assert
            var text = await state.GetTextAsync();
            Assert.Equal(0, text.Length);
        }

        [Fact]
        public async Task DocumentState_WithText_CreatesNewState()
        {
            // Arrange
            var original = DocumentState.Create(Workspace.Services, HostDocument, DocumentState.EmptyLoader);

            // Act
            var state = original.WithText(Text, VersionStamp.Create());

            // Assert
            var text = await state.GetTextAsync();
            Assert.Same(Text, text);
        }

        [Fact]
        public async Task DocumentState_WithTextLoader_CreatesNewState()
        {
            // Arrange
            var original = DocumentState.Create(Workspace.Services, HostDocument, DocumentState.EmptyLoader);

            // Act
            var state = original.WithTextLoader(TextLoader);

            // Assert
            var text = await state.GetTextAsync();
            Assert.Same(Text, text);
        }

        [Fact]
        public void DocumentState_WithConfigurationChange_CachesSnapshotText()
        {
            // Arrange
            var original = DocumentState.Create(Workspace.Services, HostDocument, DocumentState.EmptyLoader)
                .WithText(Text, VersionStamp.Create());

            // Act
            var state = original.WithConfigurationChange();

            // Assert
            Assert.True(state.TryGetText(out _));
            Assert.True(state.TryGetTextVersion(out _));
        }

        [Fact]
        public async Task DocumentState_WithConfigurationChange_CachesLoadedText()
        {
            // Arrange
            var original = DocumentState.Create(Workspace.Services, HostDocument, DocumentState.EmptyLoader)
                .WithTextLoader(TextLoader);

            await original.GetTextAsync();

            // Act
            var state = original.WithConfigurationChange();

            // Assert
            Assert.True(state.TryGetText(out _));
            Assert.True(state.TryGetTextVersion(out _));
        }

        [Fact]
        public void DocumentState_WithImportsChange_CachesSnapshotText()
        {
            // Arrange
            var original = DocumentState.Create(Workspace.Services, HostDocument, DocumentState.EmptyLoader)
                .WithText(Text, VersionStamp.Create());

            // Act
            var state = original.WithImportsChange();

            // Assert
            Assert.True(state.TryGetText(out _));
            Assert.True(state.TryGetTextVersion(out _));
        }

        [Fact]
        public async Task DocumentState_WithImportsChange_CachesLoadedText()
        {
            // Arrange
            var original = DocumentState.Create(Workspace.Services, HostDocument, DocumentState.EmptyLoader)
                .WithTextLoader(TextLoader);

            await original.GetTextAsync();

            // Act
            var state = original.WithImportsChange();

            // Assert
            Assert.True(state.TryGetText(out _));
            Assert.True(state.TryGetTextVersion(out _));
        }

        [Fact]
        public void DocumentState_WithProjectWorkspaceStateChange_CachesSnapshotText()
        {
            // Arrange
            var original = DocumentState.Create(Workspace.Services, HostDocument, DocumentState.EmptyLoader)
                .WithText(Text, VersionStamp.Create());

            // Act
            var state = original.WithProjectWorkspaceStateChange();

            // Assert
            Assert.True(state.TryGetText(out _));
            Assert.True(state.TryGetTextVersion(out _));
        }

        [Fact]
        public async Task DocumentState_WithProjectWorkspaceStateChange_CachesLoadedText()
        {
            // Arrange
            var original = DocumentState.Create(Workspace.Services, HostDocument, DocumentState.EmptyLoader)
                .WithTextLoader(TextLoader);

            await original.GetTextAsync();

            // Act
            var state = original.WithProjectWorkspaceStateChange();

            // Assert
            Assert.True(state.TryGetText(out _));
            Assert.True(state.TryGetTextVersion(out _));
        }
    }
}
