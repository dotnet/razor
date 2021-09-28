﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.IO;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.Editor.Razor.Documents
{
    public class DefaultImportDocumentManagerIntegrationTest : ProjectSnapshotManagerDispatcherTestBase
    {
        public DefaultImportDocumentManagerIntegrationTest()
        {
            ProjectPath = TestProjectData.SomeProject.FilePath;
            DirectoryPath = Path.GetDirectoryName(ProjectPath);

            FileSystem = RazorProjectFileSystem.Create(Path.GetDirectoryName(ProjectPath));
            ProjectEngine = RazorProjectEngine.Create(FallbackRazorConfiguration.MVC_2_1, FileSystem, b =>
                // These tests rely on MVC's import behavior.
                Microsoft.AspNetCore.Mvc.Razor.Extensions.RazorExtensions.Register(b));
        }

        private string DirectoryPath { get; }

        private string ProjectPath { get; }

        private RazorProjectFileSystem FileSystem { get; }

        private RazorProjectEngine ProjectEngine { get; }

        [UIFact]
        public void Changed_TrackerChanged_ResultsInChangedHavingCorrectArgs()
        {
            // Arrange
            var testImportsPath = Path.Combine(DirectoryPath, "_ViewImports.cshtml");

            var tracker = Mock.Of<VisualStudioDocumentTracker>(
                t => t.FilePath == Path.Combine(DirectoryPath, "Views", "Home", "_ViewImports.cshtml") &&
                t.ProjectPath == ProjectPath &&
                t.ProjectSnapshot == Mock.Of<ProjectSnapshot>(p => p.GetProjectEngine() == ProjectEngine && p.GetDocument(It.IsAny<string>()) == null, MockBehavior.Strict), MockBehavior.Strict);

            var anotherTracker = Mock.Of<VisualStudioDocumentTracker>(
                t => t.FilePath == Path.Combine(DirectoryPath, "anotherFile.cshtml") &&
                t.ProjectPath == ProjectPath &&
                t.ProjectSnapshot == Mock.Of<ProjectSnapshot>(p => p.GetProjectEngine() == ProjectEngine && p.GetDocument(It.IsAny<string>()) == null, MockBehavior.Strict), MockBehavior.Strict);

            var fileChangeTrackerFactory = new Mock<FileChangeTrackerFactory>(MockBehavior.Strict);
            var fileChangeTracker = new Mock<FileChangeTracker>(MockBehavior.Strict);
            fileChangeTracker
                .Setup(f => f.FilePath)
                .Returns(testImportsPath);
            fileChangeTracker.Setup(f => f.StartListening()).Verifiable();
            fileChangeTrackerFactory
                .Setup(f => f.Create(testImportsPath))
                .Returns(fileChangeTracker.Object);

            var fileChangeTracker2 = new Mock<FileChangeTracker>(MockBehavior.Strict);
            fileChangeTracker2.Setup(f => f.StartListening()).Verifiable();
            fileChangeTrackerFactory
                .Setup(f => f.Create(Path.Combine(DirectoryPath, "Views", "_ViewImports.cshtml")))
                .Returns(fileChangeTracker2.Object);
            fileChangeTrackerFactory
                .Setup(f => f.Create(Path.Combine(DirectoryPath, "Views", "Home", "_ViewImports.cshtml")))
                .Returns(Mock.Of<FileChangeTracker>(MockBehavior.Strict));

            var called = false;
            var manager = new DefaultImportDocumentManager(Dispatcher, new DefaultErrorReporter(), fileChangeTrackerFactory.Object);
            manager.OnSubscribed(tracker);
            manager.OnSubscribed(anotherTracker);
            manager.Changed += (sender, args) =>
            {
                called = true;
                Assert.Same(sender, manager);
                Assert.Equal(testImportsPath, args.FilePath);
                Assert.Equal(FileChangeKind.Changed, args.Kind);
                Assert.Collection(
                    args.AssociatedDocuments,
                    f => Assert.Equal(Path.Combine(DirectoryPath, "Views", "Home", "_ViewImports.cshtml"), f),
                    f => Assert.Equal(Path.Combine(DirectoryPath, "anotherFile.cshtml"), f));
            };

            // Act
            fileChangeTracker.Raise(t => t.Changed += null, new FileChangeEventArgs(testImportsPath, FileChangeKind.Changed));

            // Assert
            Assert.True(called);
        }
    }
}
