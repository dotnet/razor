﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Linq;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class DefaultDocumentVersionCacheTest : LanguageServerTestBase
    {
        public DefaultDocumentVersionCacheTest(ITestOutputHelper testOutput)
            : base(testOutput)
        {
        }

        [Fact]
        public void MarkAsLatestVersion_UntrackedDocument_Noops()
        {
            // Arrange
            var documentVersionCache = new DefaultDocumentVersionCache(LegacyDispatcher);
            var document = TestDocumentSnapshot.Create("C:/file.cshtml");
            documentVersionCache.TrackDocumentVersion(document, 123);
            var untrackedDocument = TestDocumentSnapshot.Create("C:/other.cshtml");

            // Act
            documentVersionCache.MarkAsLatestVersion(untrackedDocument);

            // Assert
            Assert.False(documentVersionCache.TryGetDocumentVersion(untrackedDocument, out var version));
            Assert.Null(version);
        }

        [Fact]
        public void MarkAsLatestVersion_KnownDocument_TracksNewDocumentAsLatest()
        {
            // Arrange
            var documentVersionCache = new DefaultDocumentVersionCache(LegacyDispatcher);
            var documentInitial = TestDocumentSnapshot.Create("C:/file.cshtml");
            documentVersionCache.TrackDocumentVersion(documentInitial, 123);
            var documentLatest = TestDocumentSnapshot.Create(documentInitial.FilePath);

            // Act
            documentVersionCache.MarkAsLatestVersion(documentLatest);

            // Assert
            Assert.True(documentVersionCache.TryGetDocumentVersion(documentLatest, out var version));
            Assert.Equal(123, version);
        }

        [Fact]
        public void TryGetLatestVersionFromPath_TrackedDocument_ReturnsTrue()
        {
            // Arrange
            var documentVersionCache = new DefaultDocumentVersionCache(LegacyDispatcher);
            var filePath = "C:/file.cshtml";
            var document1 = TestDocumentSnapshot.Create(filePath);
            var document2 = TestDocumentSnapshot.Create(filePath);
            documentVersionCache.TrackDocumentVersion(document1, 123);
            documentVersionCache.TrackDocumentVersion(document2, 1337);

            // Act
            var result = documentVersionCache.TryGetLatestVersionFromPath(filePath, out var version);

            // Assert
            Assert.True(result);
            Assert.Equal(1337, version);
        }

        [Fact]
        public void TryGetLatestVersionFromPath_UntrackedDocument_ReturnsFalse()
        {
            // Arrange
            var documentVersionCache = new DefaultDocumentVersionCache(LegacyDispatcher);

            // Act
            var result = documentVersionCache.TryGetLatestVersionFromPath("C:/file.cshtml", out var version);

            // Assert
            Assert.False(result);
            Assert.Null(version);
        }

        [Fact]
        public void ProjectSnapshotManager_Changed_DocumentRemoved_DoesNotEvictDocument()
        {
            // Arrange
            var documentVersionCache = new DefaultDocumentVersionCache(LegacyDispatcher);
            var projectSnapshotManager = TestProjectSnapshotManager.Create(LegacyDispatcher);
            projectSnapshotManager.AllowNotifyListeners = true;
            documentVersionCache.Initialize(projectSnapshotManager);
            var document = TestDocumentSnapshot.Create("C:/file.cshtml");
            document.TryGetText(out var text);
            document.TryGetTextVersion(out var textVersion);
            var textAndVersion = TextAndVersion.Create(text, textVersion);
            documentVersionCache.TrackDocumentVersion(document, 1337);
            projectSnapshotManager.ProjectAdded(document.ProjectInternal.HostProject);
            projectSnapshotManager.DocumentAdded(document.ProjectInternal.HostProject, document.State.HostDocument, TextLoader.From(textAndVersion));

            // Act - 1
            var result = documentVersionCache.TryGetDocumentVersion(document, out _);

            // Assert - 1
            Assert.True(result);

            // Act - 2
            projectSnapshotManager.DocumentRemoved(document.ProjectInternal.HostProject, document.State.HostDocument);
            result = documentVersionCache.TryGetDocumentVersion(document, out _);

            // Assert - 2
            Assert.True(result);
        }

        [Fact]
        public void ProjectSnapshotManager_Changed_OpenDocumentRemoved_DoesNotEvictDocument()
        {
            // Arrange
            var documentVersionCache = new DefaultDocumentVersionCache(LegacyDispatcher);
            var projectSnapshotManager = TestProjectSnapshotManager.Create(LegacyDispatcher);
            projectSnapshotManager.AllowNotifyListeners = true;
            documentVersionCache.Initialize(projectSnapshotManager);
            var document = TestDocumentSnapshot.Create("C:/file.cshtml");
            document.TryGetText(out var text);
            document.TryGetTextVersion(out var textVersion);
            var textAndVersion = TextAndVersion.Create(text, textVersion);
            documentVersionCache.TrackDocumentVersion(document, 1337);
            projectSnapshotManager.ProjectAdded(document.ProjectInternal.HostProject);
            projectSnapshotManager.DocumentAdded(document.ProjectInternal.HostProject, document.State.HostDocument, TextLoader.From(textAndVersion));
            projectSnapshotManager.DocumentOpened(document.ProjectInternal.FilePath, document.FilePath, textAndVersion.Text);

            // Act - 1
            var result = documentVersionCache.TryGetDocumentVersion(document, out _);

            // Assert - 1
            Assert.True(result);
            Assert.True(projectSnapshotManager.IsDocumentOpen(document.FilePath));

            // Act - 2
            projectSnapshotManager.DocumentRemoved(document.ProjectInternal.HostProject, document.State.HostDocument);
            result = documentVersionCache.TryGetDocumentVersion(document, out _);

            // Assert - 2
            Assert.True(result);
        }

        [Fact]
        public void ProjectSnapshotManager_Changed_DocumentClosed_EvictsDocument()
        {
            // Arrange
            var documentVersionCache = new DefaultDocumentVersionCache(LegacyDispatcher);
            var projectSnapshotManager = TestProjectSnapshotManager.Create(LegacyDispatcher);
            projectSnapshotManager.AllowNotifyListeners = true;
            documentVersionCache.Initialize(projectSnapshotManager);
            var document = TestDocumentSnapshot.Create("C:/file.cshtml");
            document.TryGetText(out var text);
            document.TryGetTextVersion(out var textVersion);
            var textAndVersion = TextAndVersion.Create(text, textVersion);
            documentVersionCache.TrackDocumentVersion(document, 1337);
            projectSnapshotManager.ProjectAdded(document.ProjectInternal.HostProject);
            var textLoader = TextLoader.From(textAndVersion);
            projectSnapshotManager.DocumentAdded(document.ProjectInternal.HostProject, document.State.HostDocument, textLoader);

            // Act - 1
            var result = documentVersionCache.TryGetDocumentVersion(document, out _);

            // Assert - 1
            Assert.True(result);

            // Act - 2
            projectSnapshotManager.DocumentClosed(document.ProjectInternal.HostProject.FilePath, document.State.HostDocument.FilePath, textLoader);
            result = documentVersionCache.TryGetDocumentVersion(document, out var version);

            // Assert - 2
            Assert.False(result);
            Assert.Null(version);
        }

        [Fact]
        public void TrackDocumentVersion_AddsFirstEntry()
        {
            // Arrange
            var documentVersionCache = new DefaultDocumentVersionCache(LegacyDispatcher);
            var document = TestDocumentSnapshot.Create("C:/file.cshtml");

            // Act
            documentVersionCache.TrackDocumentVersion(document, 1337);

            // Assert
            var kvp = Assert.Single(documentVersionCache.DocumentLookup);
            Assert.Equal(document.FilePath, kvp.Key);
            var entry = Assert.Single(kvp.Value);
            Assert.True(entry.Document.TryGetTarget(out var actualDocument));
            Assert.Same(document, actualDocument);
            Assert.Equal(1337, entry.Version);
        }

        [Fact]
        public void TrackDocumentVersion_EvictsOldEntries()
        {
            // Arrange
            var documentVersionCache = new DefaultDocumentVersionCache(LegacyDispatcher);
            var document = TestDocumentSnapshot.Create("C:/file.cshtml");

            for (var i = 0; i < DefaultDocumentVersionCache.MaxDocumentTrackingCount; i++)
            {
                documentVersionCache.TrackDocumentVersion(document, i);
            }

            // Act
            documentVersionCache.TrackDocumentVersion(document, 1337);

            // Assert
            var kvp = Assert.Single(documentVersionCache.DocumentLookup);
            Assert.Equal(DefaultDocumentVersionCache.MaxDocumentTrackingCount, kvp.Value.Count);
            Assert.Equal(1337, kvp.Value.Last().Version);
        }

        [Fact]
        public void TryGetDocumentVersion_UntrackedDocumentPath_ReturnsFalse()
        {
            // Arrange
            var documentVersionCache = new DefaultDocumentVersionCache(LegacyDispatcher);
            var document = TestDocumentSnapshot.Create("C:/file.cshtml");

            // Act
            var result = documentVersionCache.TryGetDocumentVersion(document, out var version);

            // Assert
            Assert.False(result);
            Assert.Null(version);
        }

        [Fact]
        public void TryGetDocumentVersion_EvictedDocument_ReturnsFalse()
        {
            // Arrange
            var documentVersionCache = new DefaultDocumentVersionCache(LegacyDispatcher);
            var document = TestDocumentSnapshot.Create("C:/file.cshtml");
            var evictedDocument = TestDocumentSnapshot.Create(document.FilePath);
            documentVersionCache.TrackDocumentVersion(document, 1337);

            // Act
            var result = documentVersionCache.TryGetDocumentVersion(evictedDocument, out var version);

            // Assert
            Assert.False(result);
            Assert.Null(version);
        }

        [Fact]
        public void TryGetDocumentVersion_KnownDocument_ReturnsTrue()
        {
            // Arrange
            var documentVersionCache = new DefaultDocumentVersionCache(LegacyDispatcher);
            var document = TestDocumentSnapshot.Create("C:/file.cshtml");
            documentVersionCache.TrackDocumentVersion(document, 1337);

            // Act
            var result = documentVersionCache.TryGetDocumentVersion(document, out var version);

            // Assert
            Assert.True(result);
            Assert.Equal(1337, version);
        }
    }
}
