// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem
{
    public class DefaultDocumentSnapshotTest : WorkspaceTestBase
    {
        public DefaultDocumentSnapshotTest()
        {
            SourceText = SourceText.From("<p>Hello World</p>");
            Version = VersionStamp.Create();

            // Create a new HostDocument to avoid mutating the code container
            ComponentCshtmlHostDocument = new HostDocument(TestProjectData.SomeProjectCshtmlComponentFile5);
            ComponentHostDocument = new HostDocument(TestProjectData.SomeProjectComponentFile1);
            LegacyHostDocument = new HostDocument(TestProjectData.SomeProjectFile1);
            NestedComponentHostDocument = new HostDocument(TestProjectData.SomeProjectNestedComponentFile3);

            var projectState = ProjectState.Create(Workspace.Services, TestProjectData.SomeProject);
            var project = new DefaultProjectSnapshot(projectState);

            var textAndVersion = TextAndVersion.Create(SourceText, Version);

            var documentState = DocumentState.Create(Workspace.Services, LegacyHostDocument, () => Task.FromResult(textAndVersion));
            LegacyDocument = new DefaultDocumentSnapshot(project, documentState);

            documentState = DocumentState.Create(Workspace.Services, ComponentHostDocument, () => Task.FromResult(textAndVersion));
            ComponentDocument = new DefaultDocumentSnapshot(project, documentState);

            documentState = DocumentState.Create(Workspace.Services, ComponentCshtmlHostDocument, () => Task.FromResult(textAndVersion));
            ComponentCshtmlDocument = new DefaultDocumentSnapshot(project, documentState);

            documentState = DocumentState.Create(Workspace.Services, NestedComponentHostDocument, () => Task.FromResult(textAndVersion));
            NestedComponentDocument = new DefaultDocumentSnapshot(project, documentState);
        }

        private SourceText SourceText { get; }

        private VersionStamp Version { get; }

        private HostDocument ComponentHostDocument { get; }

        private HostDocument ComponentCshtmlHostDocument { get; }

        private HostDocument LegacyHostDocument { get; }

        private DefaultDocumentSnapshot ComponentDocument { get; }

        private DefaultDocumentSnapshot ComponentCshtmlDocument { get; }

        private DefaultDocumentSnapshot LegacyDocument { get; }

        private HostDocument NestedComponentHostDocument { get; }

        private DefaultDocumentSnapshot NestedComponentDocument { get; }

        protected override void ConfigureWorkspaceServices(List<IWorkspaceService> services)
        {
            services.Add(new TestTagHelperResolver());
        }

        [Fact]
        public async Task GCCollect_OutputIsNoLongerCached()
        {
            // Arrange
            await Task.Run(async () => { await LegacyDocument.GetGeneratedOutputAsync(); });

            // Act

            // Forces collection of the cached document output
            GC.Collect();

            // Assert
            Assert.False(LegacyDocument.TryGetGeneratedOutput(out _));
        }

        [Fact]
        public async Task RegeneratingWithReference_CachesOutput()
        {
            // Arrange
            var output = await LegacyDocument.GetGeneratedOutputAsync();

            // Mostly doing this to ensure "var output" doesn't get optimized out
            Assert.NotNull(output);

            // Act & Assert
            Assert.True(LegacyDocument.TryGetGeneratedOutput(out _));
        }

        // This is a sanity test that we invoke component codegen for components.It's a little fragile but
        // necessary.

        [Fact]
        public async Task GetGeneratedOutputAsync_CshtmlComponent_ContainsComponentImports()
        {
            // Act
            var codeDocument = await ComponentCshtmlDocument.GetGeneratedOutputAsync();

            // Assert
            Assert.Contains("using Microsoft.AspNetCore.Components", codeDocument.GetCSharpSourceText().ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetGeneratedOutputAsync_Component()
        {
            // Act
            var codeDocument = await ComponentDocument.GetGeneratedOutputAsync();

            // Assert
            Assert.Contains("ComponentBase", codeDocument.GetCSharpSourceText().ToString(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetGeneratedOutputAsync_NestedComponentDocument_SetsCorrectNamespaceAndClassName()
        {
            // Act
            var codeDocument = await NestedComponentDocument.GetGeneratedOutputAsync();

            // Assert
            Assert.Contains("ComponentBase", codeDocument.GetCSharpSourceText().ToString(), StringComparison.Ordinal);
            Assert.Contains("namespace SomeProject.Nested", codeDocument.GetCSharpSourceText().ToString(), StringComparison.Ordinal);
            Assert.Contains("class File3", codeDocument.GetCSharpSourceText().ToString(), StringComparison.Ordinal);
        }

        // This is a sanity test that we invoke legacy codegen for .cshtml files. It's a little fragile but
        // necessary.
        [Fact]
        public async Task GetGeneratedOutputAsync_Legacy()
        {
            // Act
            var codeDocument = await LegacyDocument.GetGeneratedOutputAsync();

            // Assert
            Assert.Contains("Template", codeDocument.GetCSharpSourceText().ToString(), StringComparison.Ordinal);
        }
    }
}
