// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Editor.Razor.Documents;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Editor.Razor;

public class DefaultImportDocumentManagerTest : ProjectSnapshotManagerDispatcherTestBase
{
    private readonly string _projectPath;
    private readonly string _directoryPath;
    private readonly RazorProjectFileSystem _fileSystem;
    private readonly RazorProjectEngine _projectEngine;

    public DefaultImportDocumentManagerTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _projectPath = TestProjectData.SomeProject.FilePath;
        _directoryPath = Path.GetDirectoryName(_projectPath);

        _fileSystem = RazorProjectFileSystem.Create(Path.GetDirectoryName(_projectPath));
        _projectEngine = RazorProjectEngine.Create(FallbackRazorConfiguration.MVC_2_1, _fileSystem, b =>
            // These tests rely on MVC's import behavior.
            Microsoft.AspNetCore.Mvc.Razor.Extensions.RazorExtensions.Register(b));
    }

    [UIFact]
    public async Task OnSubscribed_StartsFileChangeTrackers()
    {
        // Arrange
        var tracker = Mock.Of<VisualStudioDocumentTracker>(
            t => t.FilePath == Path.Combine(_directoryPath, "Views", "Home", "file.cshtml") &&
            t.ProjectPath == _projectPath &&
            t.ProjectSnapshot == Mock.Of<IProjectSnapshot>(p => p.GetProjectEngine() == _projectEngine && p.GetDocument(It.IsAny<string>()) == null, MockBehavior.Strict), MockBehavior.Strict);

        var fileChangeTrackerFactory = new Mock<FileChangeTrackerFactory>(MockBehavior.Strict);
        var fileChangeTracker1 = new Mock<FileChangeTracker>(MockBehavior.Strict);
        fileChangeTracker1
            .Setup(f => f.StartListeningAsync(It.IsAny<CancellationToken>()))
            .Returns(default(ValueTask))
            .Verifiable();
        fileChangeTrackerFactory
            .Setup(f => f.Create(Path.Combine(_directoryPath, "Views", "Home", "_ViewImports.cshtml")))
            .Returns(fileChangeTracker1.Object)
            .Verifiable();
        var fileChangeTracker2 = new Mock<FileChangeTracker>(MockBehavior.Strict);
        fileChangeTracker2
            .Setup(f => f.StartListeningAsync(It.IsAny<CancellationToken>()))
            .Returns(default(ValueTask))
            .Verifiable();
        fileChangeTrackerFactory
            .Setup(f => f.Create(Path.Combine(_directoryPath, "Views", "_ViewImports.cshtml")))
            .Returns(fileChangeTracker2.Object)
            .Verifiable();
        var fileChangeTracker3 = new Mock<FileChangeTracker>(MockBehavior.Strict);
        fileChangeTracker3
            .Setup(f => f.StartListeningAsync(It.IsAny<CancellationToken>()))
            .Returns(default(ValueTask))
            .Verifiable();
        fileChangeTrackerFactory
            .Setup(f => f.Create(Path.Combine(_directoryPath, "_ViewImports.cshtml")))
            .Returns(fileChangeTracker3.Object)
            .Verifiable();

        var manager = new ImportDocumentManager(Dispatcher, fileChangeTrackerFactory.Object);

        // Act
        await manager.OnSubscribedAsync(tracker, DisposalToken);

        // Assert
        fileChangeTrackerFactory.Verify();
        fileChangeTracker1.Verify();
        fileChangeTracker2.Verify();
        fileChangeTracker3.Verify();
    }

    [UIFact]
    public async Task OnSubscribed_AlreadySubscribed_DoesNothing()
    {
        // Arrange
        var tracker = Mock.Of<VisualStudioDocumentTracker>(
            t => t.FilePath == Path.Combine(_directoryPath, "file.cshtml") &&
            t.ProjectPath == _projectPath &&
            t.ProjectSnapshot == Mock.Of<IProjectSnapshot>(p => p.GetProjectEngine() == _projectEngine && p.GetDocument(It.IsAny<string>()) == null, MockBehavior.Strict), MockBehavior.Strict);

        var anotherTracker = Mock.Of<VisualStudioDocumentTracker>(
            t => t.FilePath == Path.Combine(_directoryPath, "anotherFile.cshtml") &&
            t.ProjectPath == _projectPath &&
            t.ProjectSnapshot == Mock.Of<IProjectSnapshot>(p => p.GetProjectEngine() == _projectEngine && p.GetDocument(It.IsAny<string>()) == null, MockBehavior.Strict), MockBehavior.Strict);

        var callCount = 0;
        var fileChangeTrackerFactory = new Mock<FileChangeTrackerFactory>(MockBehavior.Strict);
        var fileChangeTracker = new Mock<FileChangeTracker>(MockBehavior.Strict);
        fileChangeTracker
            .Setup(f => f.StartListeningAsync(It.IsAny<CancellationToken>()))
            .Returns(default(ValueTask))
            .Verifiable();
        fileChangeTrackerFactory
            .Setup(f => f.Create(It.IsAny<string>()))
            .Returns(fileChangeTracker.Object)
            .Callback(() => callCount++);

        var manager = new ImportDocumentManager(Dispatcher, fileChangeTrackerFactory.Object);
        await manager.OnSubscribedAsync(tracker, DisposalToken); // Start tracking the import.

        // Act
        await manager.OnSubscribedAsync(anotherTracker, DisposalToken);

        // Assert
        Assert.Equal(1, callCount);
    }

    [UIFact]
    public async Task OnUnsubscribed_StopsFileChangeTracker()
    {
        // Arrange
        var tracker = Mock.Of<VisualStudioDocumentTracker>(
            t => t.FilePath == Path.Combine(_directoryPath, "file.cshtml") &&
            t.ProjectPath == _projectPath &&
            t.ProjectSnapshot == Mock.Of<IProjectSnapshot>(p => p.GetProjectEngine() == _projectEngine && p.GetDocument(It.IsAny<string>()) == null, MockBehavior.Strict), MockBehavior.Strict);

        var fileChangeTrackerFactory = new Mock<FileChangeTrackerFactory>(MockBehavior.Strict);
        var fileChangeTracker = new Mock<FileChangeTracker>(MockBehavior.Strict);
        fileChangeTracker
            .Setup(f => f.StartListeningAsync(It.IsAny<CancellationToken>()))
            .Returns(default(ValueTask))
            .Verifiable();
        fileChangeTracker
            .Setup(f => f.StopListeningAsync(It.IsAny<CancellationToken>()))
            .Returns(default(ValueTask))
            .Verifiable();
        fileChangeTrackerFactory
            .Setup(f => f.Create(Path.Combine(_directoryPath, "_ViewImports.cshtml")))
            .Returns(fileChangeTracker.Object)
            .Verifiable();

        var manager = new ImportDocumentManager(Dispatcher, fileChangeTrackerFactory.Object);
        await manager.OnSubscribedAsync(tracker, DisposalToken); // Start tracking the import.

        // Act
        await manager.OnUnsubscribedAsync(tracker, DisposalToken);

        // Assert
        fileChangeTrackerFactory.Verify();
        fileChangeTracker.Verify();
    }

    [UIFact]
    public async Task OnUnsubscribed_AnotherDocumentTrackingImport_DoesNotStopFileChangeTracker()
    {
        // Arrange
        var tracker = Mock.Of<VisualStudioDocumentTracker>(
            t => t.FilePath == Path.Combine(_directoryPath, "file.cshtml") &&
            t.ProjectPath == _projectPath &&
            t.ProjectSnapshot == Mock.Of<IProjectSnapshot>(p => p.GetProjectEngine() == _projectEngine && p.GetDocument(It.IsAny<string>()) == null, MockBehavior.Strict), MockBehavior.Strict);

        var anotherTracker = Mock.Of<VisualStudioDocumentTracker>(
            t => t.FilePath == Path.Combine(_directoryPath, "anotherFile.cshtml") &&
            t.ProjectPath == _projectPath &&
            t.ProjectSnapshot == Mock.Of<IProjectSnapshot>(p => p.GetProjectEngine() == _projectEngine && p.GetDocument(It.IsAny<string>()) == null, MockBehavior.Strict), MockBehavior.Strict);

        var fileChangeTrackerFactory = new Mock<FileChangeTrackerFactory>(MockBehavior.Strict);
        var fileChangeTracker = new Mock<FileChangeTracker>(MockBehavior.Strict);
        fileChangeTracker
            .Setup(f => f.StartListeningAsync(It.IsAny<CancellationToken>()))
            .Returns(default(ValueTask))
            .Verifiable();
        fileChangeTracker
            .Setup(f => f.StopListeningAsync(It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException());
        fileChangeTrackerFactory
            .Setup(f => f.Create(It.IsAny<string>()))
            .Returns(fileChangeTracker.Object);

        var manager = new ImportDocumentManager(Dispatcher, fileChangeTrackerFactory.Object);
        await manager.OnSubscribedAsync(tracker, DisposalToken); // Starts tracking import for the first document.

        await manager.OnSubscribedAsync(anotherTracker, DisposalToken); // Starts tracking import for the second document.

        // Act & Assert (Does not throw)
        await manager.OnUnsubscribedAsync(tracker, DisposalToken);
        await manager.OnUnsubscribedAsync(tracker, DisposalToken);
    }
}
