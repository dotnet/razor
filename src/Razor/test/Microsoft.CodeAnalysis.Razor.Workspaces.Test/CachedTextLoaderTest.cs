// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;
using Moq;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Test
{
    public class CachedTextLoaderTest
    {
        private class TestCachedTextLoader : CachedTextLoader
        {
            private readonly DateTime _time;

            public TestCachedTextLoader(DateTime time, string filePath, TextLoader? baseLoader = null) : base(filePath, baseLoader)
            {
                _time = time;
            }

            internal override DateTime? GetLastWriteTimeUtc(string filePath)
            {
                return _time;
            }
        }

        [Fact]
        public async Task CachedTextLoader_FileModified()
        {
            // Arrange
            var filePath = "Z:\\location\\file.razor";
            var sourceText = SourceText.From("sourceText");
            var firstVersion = VersionStamp.Default;
            var secondVersion = VersionStamp.Create(DateTime.UtcNow.AddHours(1));

            using var workspace = TestWorkspace.Create();
            var projectId = ProjectId.CreateNewId();
            var documentId = DocumentId.CreateNewId(projectId);

            var firstMockTextLoader = new Mock<TextLoader>(MockBehavior.Strict);
            firstMockTextLoader.Setup(t => t.LoadTextAndVersionAsync(It.IsAny<Workspace>(), It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(TextAndVersion.Create(sourceText, firstVersion, filePath)));
            var secondMockTextLoader = new Mock<TextLoader>(MockBehavior.Strict);
            secondMockTextLoader.Setup(t => t.LoadTextAndVersionAsync(It.IsAny<Workspace>(), It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(TextAndVersion.Create(sourceText, secondVersion, filePath)));

            var firstTextLoader = new TestCachedTextLoader(DateTime.Now, filePath, firstMockTextLoader.Object);
            var secondTextLoader = new TestCachedTextLoader(DateTime.Now.AddHours(1), filePath, secondMockTextLoader.Object);

            // Act
            var firstTextAndVersionTask = firstTextLoader.LoadTextAndVersionAsync(workspace, documentId, CancellationToken.None);
            var secondTextAndVersionTask = secondTextLoader.LoadTextAndVersionAsync(workspace, documentId, CancellationToken.None);

            var firstTextAndVersion = await firstTextAndVersionTask;
            var secondTextAndVersion = await secondTextAndVersionTask;

            // Assert
            Assert.NotSame(firstTextAndVersionTask, secondTextAndVersionTask);
            Assert.NotSame(firstTextAndVersion, secondTextAndVersion);
        }

        [Fact]
        public async Task CachedTextLoader_ReUsesTask()
        {
            // Arrange
            var filePath = "Z:\\location\\file.razor";
            var sourceText = SourceText.From("sourceText");
            var version = VersionStamp.Default;

            using var workspace = TestWorkspace.Create();
            var projectId = ProjectId.CreateNewId();
            var documentId = DocumentId.CreateNewId(projectId);

            var mockTextLoader = new Mock<TextLoader>(MockBehavior.Strict);
            mockTextLoader.Setup(t => t.LoadTextAndVersionAsync(It.IsAny<Workspace>(), It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(TextAndVersion.Create(sourceText, version, filePath)));

            var now = DateTime.Now;

            var firstTextLoader = new TestCachedTextLoader(now, filePath, mockTextLoader.Object);
            var secondTextLoader = new TestCachedTextLoader(now, filePath, mockTextLoader.Object);

            // Act
            var firstTextAndVersionTask = firstTextLoader.LoadTextAndVersionAsync(workspace, documentId, CancellationToken.None);
            var firstTextAndVersion = await firstTextAndVersionTask;

            var secondTextAndVersionTask = secondTextLoader.LoadTextAndVersionAsync(workspace, documentId, CancellationToken.None);
            var secondTextAndVersion = await secondTextAndVersionTask;

            // Assert
            Assert.Same(firstTextAndVersionTask, secondTextAndVersionTask);
            Assert.Equal(filePath, firstTextAndVersion.FilePath);
        }

        [Fact]
        public async Task CachedTextLoader_DifferentPaths()
        {
            // Arrange
            var firstFilePath = "Z:\\location\\file.razor";
            var secondFilePath = "Z:\\other\\location\\file.razor";

            var sourceText = SourceText.From("sourceText");
            var version = VersionStamp.Default;

            using var workspace = TestWorkspace.Create();
            var projectId = ProjectId.CreateNewId();
            var documentId = DocumentId.CreateNewId(projectId);

            var firstMockTextLoader = new Mock<TextLoader>(MockBehavior.Strict);
            firstMockTextLoader.Setup(t => t.LoadTextAndVersionAsync(It.IsAny<Workspace>(), It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(TextAndVersion.Create(sourceText, version, firstFilePath)));
            var secondMockTextLoader = new Mock<TextLoader>(MockBehavior.Strict);
            secondMockTextLoader.Setup(t => t.LoadTextAndVersionAsync(It.IsAny<Workspace>(), It.IsAny<DocumentId>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(TextAndVersion.Create(sourceText, version, secondFilePath)));

            var now = DateTime.Now;

            var firstCachedLoader = new TestCachedTextLoader(now, firstFilePath, firstMockTextLoader.Object);
            var secondCachedLoader = new TestCachedTextLoader(now, secondFilePath, secondMockTextLoader.Object);

            // Act
            var firstTextAndVersion = firstCachedLoader.LoadTextAndVersionAsync(workspace, documentId, CancellationToken.None);
            var secondTextAndVersion = secondCachedLoader.LoadTextAndVersionAsync(workspace, documentId, CancellationToken.None);

            // Assert
            Assert.NotSame(firstTextAndVersion, secondTextAndVersion);
            Assert.Equal(firstFilePath, (await firstTextAndVersion).FilePath);
            Assert.Equal(secondFilePath, (await secondTextAndVersion).FilePath);
        }
    }
}
