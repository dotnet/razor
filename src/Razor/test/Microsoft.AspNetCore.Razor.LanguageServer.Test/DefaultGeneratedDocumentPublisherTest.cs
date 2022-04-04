﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class DefaultGeneratedDocumentPublisherTest : LanguageServerTestBase
    {
        public DefaultGeneratedDocumentPublisherTest()
        {
            ServerClient = new TestClient();
            ProjectManager = TestProjectSnapshotManager.Create(LegacyDispatcher);
            ProjectManager.AllowNotifyListeners = true;
            HostProject = new HostProject("/path/to/project.csproj", RazorConfiguration.Default, "TestRootNamespace");
            ProjectManager.ProjectAdded(HostProject);
            HostDocument = new HostDocument("/path/to/file.razor", "file.razor");
            ProjectManager.DocumentAdded(HostProject, HostDocument, new EmptyTextLoader(HostDocument.FilePath));
        }

        private TestClient ServerClient { get; }

        private TestProjectSnapshotManager ProjectManager { get; }

        private HostProject HostProject { get; }

        private HostDocument HostDocument { get; }

        [Fact]
        public void PublishCSharp_FirstTime_PublishesEntireSourceText()
        {
            // Arrange
            var generatedDocumentPublisher = new DefaultGeneratedDocumentPublisher(LegacyDispatcher, ServerClient, LoggerFactory);
            var content = "// C# content";
            var sourceText = SourceText.From(content);

            // Act
            generatedDocumentPublisher.PublishCSharp("/path/to/file.razor", sourceText, 123);

            // Assert
            var updateRequest = Assert.Single(ServerClient.UpdateRequests);
            Assert.Equal("/path/to/file.razor", updateRequest.HostDocumentFilePath);
            var textChange = Assert.Single(updateRequest.Changes);
            Assert.Equal(content, textChange.NewText);
            Assert.Equal(123, updateRequest.HostDocumentVersion);
        }

        [Fact]
        public void PublishHtml_FirstTime_PublishesEntireSourceText()
        {
            // Arrange
            var generatedDocumentPublisher = new DefaultGeneratedDocumentPublisher(LegacyDispatcher, ServerClient, LoggerFactory);
            var content = "HTML content";
            var sourceText = SourceText.From(content);

            // Act
            generatedDocumentPublisher.PublishHtml("/path/to/file.razor", sourceText, 123);

            // Assert
            var updateRequest = Assert.Single(ServerClient.UpdateRequests);
            Assert.Equal("/path/to/file.razor", updateRequest.HostDocumentFilePath);
            var textChange = Assert.Single(updateRequest.Changes);
            Assert.Equal(content, textChange.NewText);
            Assert.Equal(123, updateRequest.HostDocumentVersion);
        }

        [Fact]
        public void PublishCSharp_SecondTime_PublishesSourceTextDifferences()
        {
            // Arrange
            var generatedDocumentPublisher = new DefaultGeneratedDocumentPublisher(LegacyDispatcher, ServerClient, LoggerFactory);
            var initialSourceText = SourceText.From("// Initial content\n");
            generatedDocumentPublisher.PublishCSharp("/path/to/file.razor", initialSourceText, 123);
            var change = new TextChange(
                new TextSpan(initialSourceText.Length, 0),
                "// Another line");
            var changedSourceText = initialSourceText.WithChanges(change);

            // Act
            generatedDocumentPublisher.PublishCSharp("/path/to/file.razor", changedSourceText, 124);

            // Assert
            Assert.Equal(2, ServerClient.UpdateRequests.Count);
            var updateRequest = ServerClient.UpdateRequests.Last();
            Assert.Equal("/path/to/file.razor", updateRequest.HostDocumentFilePath);
            var textChange = Assert.Single(updateRequest.Changes);
            Assert.Equal(change, textChange);
            Assert.Equal(124, updateRequest.HostDocumentVersion);
        }

        [Fact]
        public void PublishHtml_SecondTime_PublishesSourceTextDifferences()
        {
            // Arrange
            var generatedDocumentPublisher = new DefaultGeneratedDocumentPublisher(LegacyDispatcher, ServerClient, LoggerFactory);
            var initialSourceText = SourceText.From("HTML content\n");
            generatedDocumentPublisher.PublishHtml("/path/to/file.razor", initialSourceText, 123);
            var change = new TextChange(
                new TextSpan(initialSourceText.Length, 0),
                "More content!!");
            var changedSourceText = initialSourceText.WithChanges(change);

            // Act
            generatedDocumentPublisher.PublishHtml("/path/to/file.razor", changedSourceText, 124);

            // Assert
            Assert.Equal(2, ServerClient.UpdateRequests.Count);
            var updateRequest = ServerClient.UpdateRequests.Last();
            Assert.Equal("/path/to/file.razor", updateRequest.HostDocumentFilePath);
            var textChange = Assert.Single(updateRequest.Changes);
            Assert.Equal(change, textChange);
            Assert.Equal(124, updateRequest.HostDocumentVersion);
        }

        [Fact]
        public void PublishCSharp_SecondTime_IdenticalContent_NoTextChanges()
        {
            // Arrange
            var generatedDocumentPublisher = new DefaultGeneratedDocumentPublisher(LegacyDispatcher, ServerClient, LoggerFactory);
            var sourceTextContent = "// The content";
            var initialSourceText = SourceText.From(sourceTextContent);
            generatedDocumentPublisher.PublishCSharp("/path/to/file.razor", initialSourceText, 123);
            var identicalSourceText = SourceText.From(sourceTextContent);

            // Act
            generatedDocumentPublisher.PublishCSharp("/path/to/file.razor", identicalSourceText, 124);

            // Assert
            Assert.Equal(2, ServerClient.UpdateRequests.Count);
            var updateRequest = ServerClient.UpdateRequests.Last();
            Assert.Equal("/path/to/file.razor", updateRequest.HostDocumentFilePath);
            Assert.Empty(updateRequest.Changes);
            Assert.Equal(124, updateRequest.HostDocumentVersion);
        }

        [Fact]
        public void PublishHtml_SecondTime_IdenticalContent_NoTextChanges()
        {
            // Arrange
            var generatedDocumentPublisher = new DefaultGeneratedDocumentPublisher(LegacyDispatcher, ServerClient, LoggerFactory);
            var sourceTextContent = "HTMl content";
            var initialSourceText = SourceText.From(sourceTextContent);
            generatedDocumentPublisher.PublishHtml("/path/to/file.razor", initialSourceText, 123);
            var identicalSourceText = SourceText.From(sourceTextContent);

            // Act
            generatedDocumentPublisher.PublishHtml("/path/to/file.razor", identicalSourceText, 124);

            // Assert
            Assert.Equal(2, ServerClient.UpdateRequests.Count);
            var updateRequest = ServerClient.UpdateRequests.Last();
            Assert.Equal("/path/to/file.razor", updateRequest.HostDocumentFilePath);
            Assert.Empty(updateRequest.Changes);
            Assert.Equal(124, updateRequest.HostDocumentVersion);
        }

        [Fact]
        public void PublishCSharp_DifferentFileSameContent_PublishesEverything()
        {
            // Arrange
            var generatedDocumentPublisher = new DefaultGeneratedDocumentPublisher(LegacyDispatcher, ServerClient, LoggerFactory);
            var sourceTextContent = "// The content";
            var initialSourceText = SourceText.From(sourceTextContent);
            generatedDocumentPublisher.PublishCSharp("/path/to/file1.razor", initialSourceText, 123);
            var identicalSourceText = SourceText.From(sourceTextContent);

            // Act
            generatedDocumentPublisher.PublishCSharp("/path/to/file2.razor", identicalSourceText, 123);

            // Assert
            Assert.Equal(2, ServerClient.UpdateRequests.Count);
            var updateRequest = ServerClient.UpdateRequests.Last();
            Assert.Equal("/path/to/file2.razor", updateRequest.HostDocumentFilePath);
            var textChange = Assert.Single(updateRequest.Changes);
            Assert.Equal(sourceTextContent, textChange.NewText);
            Assert.Equal(123, updateRequest.HostDocumentVersion);
        }

        [Fact]
        public void PublishHtml_DifferentFileSameContent_PublishesEverything()
        {
            // Arrange
            var generatedDocumentPublisher = new DefaultGeneratedDocumentPublisher(LegacyDispatcher, ServerClient, LoggerFactory);
            var sourceTextContent = "HTML content";
            var initialSourceText = SourceText.From(sourceTextContent);
            generatedDocumentPublisher.PublishHtml("/path/to/file1.razor", initialSourceText, 123);
            var identicalSourceText = SourceText.From(sourceTextContent);

            // Act
            generatedDocumentPublisher.PublishHtml("/path/to/file2.razor", identicalSourceText, 123);

            // Assert
            Assert.Equal(2, ServerClient.UpdateRequests.Count);
            var updateRequest = ServerClient.UpdateRequests.Last();
            Assert.Equal("/path/to/file2.razor", updateRequest.HostDocumentFilePath);
            var textChange = Assert.Single(updateRequest.Changes);
            Assert.Equal(sourceTextContent, textChange.NewText);
            Assert.Equal(123, updateRequest.HostDocumentVersion);
        }

        [Fact]
        public void ProjectSnapshotManager_DocumentChanged_OpenDocument_PublishesEmptyTextChanges_CSharp()
        {
            // Arrange
            var generatedDocumentPublisher = new DefaultGeneratedDocumentPublisher(LegacyDispatcher, ServerClient, LoggerFactory);
            generatedDocumentPublisher.Initialize(ProjectManager);
            var sourceTextContent = "// The content";
            var initialSourceText = SourceText.From(sourceTextContent);
            generatedDocumentPublisher.PublishCSharp(HostDocument.FilePath, initialSourceText, 123);

            // Act
            ProjectManager.DocumentOpened(HostProject.FilePath, HostDocument.FilePath, initialSourceText);
            generatedDocumentPublisher.PublishCSharp(HostDocument.FilePath, initialSourceText, 124);

            // Assert
            Assert.Equal(2, ServerClient.UpdateRequests.Count);
            var updateRequest = ServerClient.UpdateRequests.Last();
            Assert.Equal(HostDocument.FilePath, updateRequest.HostDocumentFilePath);
            Assert.Empty(updateRequest.Changes);
            Assert.Equal(124, updateRequest.HostDocumentVersion);
        }

        [Fact]
        public void ProjectSnapshotManager_DocumentChanged_OpenDocument_VersionEquivalent_Noops_CSharp()
        {
            // Arrange
            var generatedDocumentPublisher = new DefaultGeneratedDocumentPublisher(LegacyDispatcher, ServerClient, LoggerFactory);
            generatedDocumentPublisher.Initialize(ProjectManager);
            var sourceTextContent = "// The content";
            var initialSourceText = SourceText.From(sourceTextContent);
            generatedDocumentPublisher.PublishCSharp(HostDocument.FilePath, initialSourceText, 123);

            // Act
            ProjectManager.DocumentOpened(HostProject.FilePath, HostDocument.FilePath, initialSourceText);
            generatedDocumentPublisher.PublishCSharp(HostDocument.FilePath, initialSourceText, 123);

            // Assert
            var updateRequest = Assert.Single(ServerClient.UpdateRequests);
            Assert.Equal(HostDocument.FilePath, updateRequest.HostDocumentFilePath);
            Assert.Equal(123, updateRequest.HostDocumentVersion);
        }

        [Fact]
        public void ProjectSnapshotManager_DocumentChanged_OpenDocument_PublishesEmptyTextChanges_Html()
        {
            // Arrange
            var generatedDocumentPublisher = new DefaultGeneratedDocumentPublisher(LegacyDispatcher, ServerClient, LoggerFactory);
            generatedDocumentPublisher.Initialize(ProjectManager);
            var sourceTextContent = "<!-- The content -->";
            var initialSourceText = SourceText.From(sourceTextContent);
            generatedDocumentPublisher.PublishHtml(HostDocument.FilePath, initialSourceText, 123);

            // Act
            ProjectManager.DocumentOpened(HostProject.FilePath, HostDocument.FilePath, initialSourceText);
            generatedDocumentPublisher.PublishHtml(HostDocument.FilePath, initialSourceText, 124);

            // Assert
            Assert.Equal(2, ServerClient.UpdateRequests.Count);
            var updateRequest = ServerClient.UpdateRequests.Last();
            Assert.Equal(HostDocument.FilePath, updateRequest.HostDocumentFilePath);
            Assert.Empty(updateRequest.Changes);
            Assert.Equal(124, updateRequest.HostDocumentVersion);
        }

        [Fact]
        public void ProjectSnapshotManager_DocumentChanged_OpenDocument_VersionEquivalent_Noops_Html()
        {
            // Arrange
            var generatedDocumentPublisher = new DefaultGeneratedDocumentPublisher(LegacyDispatcher, ServerClient, LoggerFactory);
            generatedDocumentPublisher.Initialize(ProjectManager);
            var sourceTextContent = "<!-- The content -->";
            var initialSourceText = SourceText.From(sourceTextContent);
            generatedDocumentPublisher.PublishHtml(HostDocument.FilePath, initialSourceText, 123);

            // Act
            ProjectManager.DocumentOpened(HostProject.FilePath, HostDocument.FilePath, initialSourceText);
            generatedDocumentPublisher.PublishHtml(HostDocument.FilePath, initialSourceText, 123);

            // Assert
            var updateRequest = Assert.Single(ServerClient.UpdateRequests);
            Assert.Equal(HostDocument.FilePath, updateRequest.HostDocumentFilePath);
            Assert.Equal(123, updateRequest.HostDocumentVersion);
        }

        [Fact]
        public void ProjectSnapshotManager_DocumentChanged_ClosedDocument_RepublishesTextChanges()
        {
            // Arrange
            var generatedDocumentPublisher = new DefaultGeneratedDocumentPublisher(LegacyDispatcher, ServerClient, LoggerFactory);
            generatedDocumentPublisher.Initialize(ProjectManager);
            var sourceTextContent = "// The content";
            var initialSourceText = SourceText.From(sourceTextContent);
            generatedDocumentPublisher.PublishCSharp(HostDocument.FilePath, initialSourceText, 123);
            ProjectManager.DocumentOpened(HostProject.FilePath, HostDocument.FilePath, initialSourceText);

            // Act
            ProjectManager.DocumentClosed(HostProject.FilePath, HostDocument.FilePath, new EmptyTextLoader(HostDocument.FilePath));
            generatedDocumentPublisher.PublishCSharp(HostDocument.FilePath, initialSourceText, 123);

            // Assert
            Assert.Equal(2, ServerClient.UpdateRequests.Count);
            var updateRequest = ServerClient.UpdateRequests.Last();
            Assert.Equal(HostDocument.FilePath, updateRequest.HostDocumentFilePath);
            var textChange = Assert.Single(updateRequest.Changes);
            Assert.Equal(sourceTextContent, textChange.NewText);
            Assert.Equal(123, updateRequest.HostDocumentVersion);
        }
    }
}
