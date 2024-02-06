﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Editor;
using Microsoft.AspNetCore.Razor.Test.Common.ProjectSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Editor.Razor.Documents;

public class EditorDocumentManagerListenerTest : ProjectSnapshotManagerDispatcherTestBase
{
    private readonly string _projectFilePath;
    private readonly ProjectKey _projectKey;
    private readonly string _documentFilePath;
    private readonly TextLoader _textLoader;
    private readonly IFileChangeTracker _fileChangeTracker;
    private readonly TestTextBuffer _textBuffer;

    public EditorDocumentManagerListenerTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _projectFilePath = TestProjectData.SomeProject.FilePath;
        _projectKey = TestProjectData.SomeProject.Key;
        _documentFilePath = TestProjectData.SomeProjectFile1.FilePath;
        _textLoader = TextLoader.From(TextAndVersion.Create(SourceText.From("FILE"), VersionStamp.Default));
        _fileChangeTracker = Mock.Of<IFileChangeTracker>(x => x.FilePath == _documentFilePath, MockBehavior.Strict);

        _textBuffer = new TestTextBuffer(new StringTextSnapshot("Hello"));
    }

    [Fact]
    public async Task ProjectManager_Changed_DocumentRemoved_RemovesDocument()
    {
        // Arrange
        var changedOnDisk = new EventHandler((o, args) => { });
        var changedInEditor = new EventHandler((o, args) => { });
        var opened = new EventHandler((o, args) => { });
        var closed = new EventHandler((o, args) => { });

        var editorDocumentManger = new Mock<IEditorDocumentManager>(MockBehavior.Strict);
        var document = GetEditorDocument(documentManager: editorDocumentManger.Object);
        editorDocumentManger
            .Setup(e => e.TryGetDocument(It.IsAny<DocumentKey>(), out document))
            .Returns(true);
        editorDocumentManger
            .Setup(e => e.RemoveDocument(It.IsAny<EditorDocument>()))
            .Callback<EditorDocument>(doc => Assert.Same(document, doc));

        var listener = new EditorDocumentManagerListener(
            editorDocumentManger.Object, Dispatcher, JoinableTaskContext, changedOnDisk, changedInEditor, opened, closed);

        var projectFilePath = "/Path/to/project.csproj";
        var project = Mock.Of<IProjectSnapshot>(p => p.Key == TestProjectKey.Create("/Path/to/obj") && p.FilePath == projectFilePath, MockBehavior.Strict);

        // Act & Assert
        await listener.ProjectManager_ChangedAsync(new ProjectChangeEventArgs(project, project, "/Path/to/file", ProjectChangeKind.DocumentRemoved, solutionIsClosing: false), DisposalToken);
    }

    [Fact]
    public async Task ProjectManager_Changed_ProjectRemoved_RemovesAllDocuments()
    {
        // Arrange
        var changedOnDisk = new EventHandler((o, args) => { });
        var changedInEditor = new EventHandler((o, args) => { });
        var opened = new EventHandler((o, args) => { });
        var closed = new EventHandler((o, args) => { });

        var editorDocumentManger = new Mock<IEditorDocumentManager>(MockBehavior.Strict);
        var document = GetEditorDocument(documentManager: editorDocumentManger.Object);
        editorDocumentManger
            .Setup(e => e.TryGetDocument(It.IsAny<DocumentKey>(), out document))
            .Returns(true);
        editorDocumentManger
            .Setup(e => e.RemoveDocument(It.IsAny<EditorDocument>()))
            .Callback<EditorDocument>(doc => Assert.Same(document, doc));

        var listener = new EditorDocumentManagerListener(
            editorDocumentManger.Object, Dispatcher, JoinableTaskContext, changedOnDisk, changedInEditor, opened, closed);

        var projectFilePath = "/Path/to/project.csproj";
        var project = Mock.Of<IProjectSnapshot>(p =>
            p.Key == TestProjectKey.Create("/Path/to/obj") &&
            p.DocumentFilePaths == new string[] { document.DocumentFilePath } &&
            p.FilePath == projectFilePath, MockBehavior.Strict);

        // Act & Assert
        await listener.ProjectManager_ChangedAsync(new ProjectChangeEventArgs(project, project, "/Path/to/file", ProjectChangeKind.DocumentRemoved, solutionIsClosing: false), DisposalToken);
    }

    [Fact]
    public async Task ProjectManager_Changed_DocumentAdded_InvokesGetOrCreateDocument()
    {
        // Arrange
        var changedOnDisk = new EventHandler((o, args) => { });
        var changedInEditor = new EventHandler((o, args) => { });
        var opened = new EventHandler((o, args) => { });
        var closed = new EventHandler((o, args) => { });

        var editorDocumentManger = new Mock<IEditorDocumentManager>(MockBehavior.Strict);
        editorDocumentManger
            .Setup(e => e.GetOrCreateDocument(It.IsAny<DocumentKey>(), It.IsAny<string>(), It.IsAny<ProjectKey>(), It.IsAny<EventHandler>(), It.IsAny<EventHandler>(), It.IsAny<EventHandler>(), It.IsAny<EventHandler>()))
            .Returns(GetEditorDocument())
            .Callback<DocumentKey, string, ProjectKey, EventHandler, EventHandler, EventHandler, EventHandler>((key, filePath, projectKey, onChangedOnDisk, onChangedInEditor, onOpened, onClosed) =>
            {
                Assert.Same(changedOnDisk, onChangedOnDisk);
                Assert.Same(changedInEditor, onChangedInEditor);
                Assert.Same(opened, onOpened);
                Assert.Same(closed, onClosed);
            });

        var listener = new EditorDocumentManagerListener(
            editorDocumentManger.Object, Dispatcher, JoinableTaskContext, changedOnDisk, changedInEditor, opened, closed);

        var projectFilePath = "/Path/to/project.csproj";
        var project = Mock.Of<IProjectSnapshot>(p => p.Key == TestProjectKey.Create("/Path/to/obj") && p.FilePath == projectFilePath, MockBehavior.Strict);

        // Act & Assert
        await listener.ProjectManager_ChangedAsync(new ProjectChangeEventArgs(project, project, "/Path/to/file", ProjectChangeKind.DocumentAdded, solutionIsClosing: false), DisposalToken);
    }

    [Fact]
    public async Task ProjectManager_Changed_OpenDocumentAdded_InvokesOnOpened()
    {
        // Arrange
        var called = false;
        var opened = new EventHandler((o, args) => called = true);

        var editorDocumentManger = new Mock<IEditorDocumentManager>(MockBehavior.Strict);
        editorDocumentManger
            .Setup(e => e.GetOrCreateDocument(It.IsAny<DocumentKey>(), It.IsAny<string>(), It.IsAny<ProjectKey>(), It.IsAny<EventHandler>(), It.IsAny<EventHandler>(), It.IsAny<EventHandler>(), It.IsAny<EventHandler>()))
            .Returns(GetEditorDocument(isOpen: true));

        var listener = new EditorDocumentManagerListener(
            editorDocumentManger.Object, Dispatcher, JoinableTaskContext, onChangedOnDisk: null, onChangedInEditor: null, onOpened: opened, onClosed: null);

        var projectFilePath = "/Path/to/project.csproj";
        var project = Mock.Of<IProjectSnapshot>(p => p.Key == TestProjectKey.Create("/Path/to/obj") && p.FilePath == projectFilePath, MockBehavior.Strict);

        // Act
        await listener.ProjectManager_ChangedAsync(new ProjectChangeEventArgs(project, project, "/Path/to/file", ProjectChangeKind.DocumentAdded, solutionIsClosing: false), DisposalToken);

        // Assert
        Assert.True(called);
    }

    private EditorDocument GetEditorDocument(bool isOpen = false, IEditorDocumentManager? documentManager = null)
    {
        var document = new EditorDocument(
            documentManager ?? Mock.Of<IEditorDocumentManager>(MockBehavior.Strict),
            Dispatcher,
            JoinableTaskContext,
            _projectFilePath,
            _documentFilePath,
            _projectKey,
            _textLoader,
            _fileChangeTracker,
            isOpen ? _textBuffer : null,
            changedOnDisk: null,
            changedInEditor: null,
            opened: null,
            closed: null);

        return document;
    }
}
