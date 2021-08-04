﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.Editor.Razor.Documents
{
    public class EditorDocumentTest : ProjectSnapshotManagerDispatcherTestBase
    {
        public EditorDocumentTest()
        {
            DocumentManager = new Mock<EditorDocumentManager>(MockBehavior.Strict).Object;
            Mock.Get(DocumentManager).Setup(m => m.RemoveDocument(It.IsAny<EditorDocument>())).Verifiable();
            ProjectFilePath = TestProjectData.SomeProject.FilePath;
            DocumentFilePath = TestProjectData.SomeProjectFile1.FilePath;
            TextLoader = TextLoader.From(TextAndVersion.Create(SourceText.From("FILE"), VersionStamp.Default));
            FileChangeTracker = new DefaultFileChangeTracker(DocumentFilePath);

            TextBuffer = new TestTextBuffer(new StringTextSnapshot("Hello"));
        }

        private EditorDocumentManager DocumentManager { get; }

        private string ProjectFilePath { get; }

        private string DocumentFilePath { get; }

        private TextLoader TextLoader { get; }

        private FileChangeTracker FileChangeTracker { get; }

        private TestTextBuffer TextBuffer { get; }

        [Fact]
        public void EditorDocument_CreatedWhileOpened()
        {
            // Arrange & Act
            using (var document = new EditorDocument(
                DocumentManager,
                Dispatcher,
                JoinableTaskFactory.Context,
                ProjectFilePath,
                DocumentFilePath,
                TextLoader,
                FileChangeTracker,
                TextBuffer,
                changedOnDisk: null,
                changedInEditor: null,
                opened: null,
                closed: null))
            {
                // Assert
                Assert.True(document.IsOpenInEditor);
                Assert.Same(TextBuffer, document.EditorTextBuffer);
                Assert.NotNull(document.EditorTextContainer);
            }
        }

        [Fact]
        public void EditorDocument_CreatedWhileClosed()
        {
            // Arrange & Act
            using (var document = new EditorDocument(
                DocumentManager,
                Dispatcher,
                JoinableTaskFactory.Context,
                ProjectFilePath,
                DocumentFilePath,
                TextLoader,
                FileChangeTracker,
                null,
                changedOnDisk: null,
                changedInEditor: null,
                opened: null,
                closed: null))
            {
                // Assert
                Assert.False(document.IsOpenInEditor);
                Assert.Null(document.EditorTextBuffer);
                Assert.Null(document.EditorTextContainer);
            }
        }
    }
}
