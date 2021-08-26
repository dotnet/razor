// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    public class DefaultLSPDocumentManagerTest
    {
        public DefaultLSPDocumentManagerTest()
        {
            var contentType = Mock.Of<IContentType>(contentType =>
                contentType.IsOfType("inert") == false &&
                contentType.IsOfType("test") == true &&
                contentType.TypeName == "test",
                MockBehavior.Strict);
            ChangeListeners = Enumerable.Empty<Lazy<LSPDocumentChangeListener, IContentTypeMetadata>>();
            JoinableTaskContext = new JoinableTaskContext();
            TextBuffer = new TestTextBuffer(new StringTextSnapshot(string.Empty));
            TextBuffer.ChangeContentType(contentType, editTag: null);
            var snapshot = TextBuffer.CurrentSnapshot;

            Uri = new Uri("C:/path/to/file.razor");
            UriProvider = Mock.Of<FileUriProvider>(provider => provider.GetOrCreate(TextBuffer) == Uri, MockBehavior.Strict);
            Mock.Get(UriProvider).Setup(p => p.Remove(It.IsAny<ITextBuffer>())).Verifiable();
            var testVirtualDocument = new TestVirtualDocument();
            var lspDocument = new DefaultLSPDocument(Uri, TextBuffer, new[] { testVirtualDocument });
            LSPDocumentSnapshot = lspDocument.CurrentSnapshot;
            LSPDocument = lspDocument;
            LSPDocumentFactory = Mock.Of<LSPDocumentFactory>(factory => factory.Create(TextBuffer) == LSPDocument, MockBehavior.Strict);
        }

        private IEnumerable<Lazy<LSPDocumentChangeListener, IContentTypeMetadata>> ChangeListeners { get; }

        private JoinableTaskContext JoinableTaskContext { get; }

        private ITextBuffer TextBuffer { get; }

        private Uri Uri { get; }

        private FileUriProvider UriProvider { get; }

        private LSPDocumentFactory LSPDocumentFactory { get; }

        private LSPDocument LSPDocument { get; }

        private LSPDocumentSnapshot LSPDocumentSnapshot { get; }

        [Fact]
        public void TrackDocument_TriggersDocumentAdded()
        {
            // Arrange
            var changeListenerLazy = CreateChangeListenerForContentTypes(new[] { LSPDocumentSnapshot.Snapshot.ContentType.TypeName });

            var changeListenerMock = Mock.Get(changeListenerLazy.Value);
            changeListenerMock.Setup(l => l.Changed(null, LSPDocumentSnapshot, It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), LSPDocumentChangeKind.Added));

            var manager = new DefaultLSPDocumentManager(JoinableTaskContext, UriProvider, LSPDocumentFactory, new[] { changeListenerLazy });

            // Act
            manager.TrackDocument(TextBuffer);

            // Assert
            changeListenerMock.Verify(l => l.Changed(null, LSPDocumentSnapshot, It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), LSPDocumentChangeKind.Added),
                                           Times.Once);
        }

        [Fact]
        public void UntrackDocument_TriggersDocumentRemoved()
        {
            // Arrange
            var changeListenerLazy = CreateChangeListenerForContentTypes(new[] { LSPDocumentSnapshot.Snapshot.ContentType.TypeName });

            var changeListenerMock = Mock.Get(changeListenerLazy.Value);
            changeListenerMock.Setup(l => l.Changed(null, LSPDocumentSnapshot, It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), LSPDocumentChangeKind.Added));
            changeListenerMock.Setup(l => l.Changed(LSPDocumentSnapshot, null, It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), LSPDocumentChangeKind.Removed));

            var manager = new DefaultLSPDocumentManager(JoinableTaskContext, UriProvider, LSPDocumentFactory, new[] { changeListenerLazy });

            manager.TrackDocument(TextBuffer);

            // We're untracking which is typically paired with the buffer going to the inert content type, lets emulate that to ensure document removed happens.
            TextBuffer.ChangeContentType(TestInertContentType.Instance, editTag: false);

            // Act
            manager.UntrackDocument(TextBuffer);

            // Assert
            changeListenerMock.Verify(l => l.Changed(LSPDocumentSnapshot, null, It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), LSPDocumentChangeKind.Removed),
                                           Times.Once);
        }

        [Fact]
        public void UpdateVirtualDocument_Noops_UnknownDocument()
        {
            // Arrange
            var changeListenerLazy = CreateChangeListenerForContentTypes(new[] { LSPDocumentSnapshot.Snapshot.ContentType.TypeName });

            var changeListenerMock = Mock.Get(changeListenerLazy.Value);
            changeListenerMock.Setup(l => l.Changed(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<LSPDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<LSPDocumentChangeKind>()));

            var manager = new DefaultLSPDocumentManager(JoinableTaskContext, UriProvider, LSPDocumentFactory, new[] { changeListenerLazy });
            var changes = new[] { new VisualStudioTextChange(1, 1, string.Empty) };

            // Act
            manager.UpdateVirtualDocument<TestVirtualDocument>(Uri, changes, 123);

            // Assert
            changeListenerMock.Verify(l => l.Changed(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<LSPDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<LSPDocumentChangeKind>()),
                                           Times.Never);
        }

        [Fact]
        public void UpdateVirtualDocument_Noops_NoChangesSameVersion()
        {
            // Arrange
            var changeListenerLazy = CreateChangeListenerForContentTypes(new[] { LSPDocumentSnapshot.Snapshot.ContentType.TypeName });

            var changeListenerMock = Mock.Get(changeListenerLazy.Value);
            changeListenerMock.Setup(l => l.Changed(null, LSPDocumentSnapshot, It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), LSPDocumentChangeKind.Added));
            changeListenerMock.Setup(l => l.Changed(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<LSPDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), LSPDocumentChangeKind.VirtualDocumentChanged));

            var manager = new DefaultLSPDocumentManager(JoinableTaskContext, UriProvider, LSPDocumentFactory, new[] { changeListenerLazy });
            manager.TrackDocument(TextBuffer);

            var changes = Array.Empty<ITextChange>();

            // Act
            manager.UpdateVirtualDocument<TestVirtualDocument>(Uri, changes, 123);

            // Assert
            changeListenerMock.Verify(l => l.Changed(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<LSPDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), LSPDocumentChangeKind.VirtualDocumentChanged),
                                           Times.Never);
        }

        [Fact]
        public void UpdateVirtualDocument_InvokesVirtualDocumentChanged()
        {
            // Arrange
            var changeListenerLazy = CreateChangeListenerForContentTypes(new[] { LSPDocumentSnapshot.Snapshot.ContentType.TypeName });

            var changeListenerMock = Mock.Get(changeListenerLazy.Value);
            changeListenerMock.Setup(l => l.Changed(null, LSPDocumentSnapshot, It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), LSPDocumentChangeKind.Added));
            changeListenerMock.Setup(l => l.Changed(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<LSPDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), LSPDocumentChangeKind.VirtualDocumentChanged));

            var manager = new DefaultLSPDocumentManager(JoinableTaskContext, UriProvider, LSPDocumentFactory, new[] { changeListenerLazy });
            manager.TrackDocument(TextBuffer);

            var changes = new[] { new VisualStudioTextChange(1, 1, string.Empty) };

            // Act
            manager.UpdateVirtualDocument<TestVirtualDocument>(Uri, changes, 123);

            // Assert
            changeListenerMock.Verify(l => l.Changed(It.IsAny<LSPDocumentSnapshot>(), It.IsAny<LSPDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), It.IsAny<VirtualDocumentSnapshot>(), LSPDocumentChangeKind.VirtualDocumentChanged),
                                           Times.Once);
        }

        [Fact]
        public void TryGetDocument_TrackedDocument_ReturnsTrue()
        {
            // Arrange
            var manager = new DefaultLSPDocumentManager(JoinableTaskContext, UriProvider, LSPDocumentFactory, ChangeListeners);
            manager.TrackDocument(TextBuffer);

            // Act
            var result = manager.TryGetDocument(Uri, out var lspDocument);

            // Assert
            Assert.True(result);
            Assert.Same(LSPDocumentSnapshot, lspDocument);
        }

        [Fact]
        public void TryGetDocument_UnknownDocument_ReturnsFalse()
        {
            // Arrange
            var manager = new DefaultLSPDocumentManager(JoinableTaskContext, UriProvider, LSPDocumentFactory, ChangeListeners);

            // Act
            var result = manager.TryGetDocument(Uri, out var lspDocument);

            // Assert
            Assert.False(result);
            Assert.Null(lspDocument);
        }

        [Fact]
        public void TryGetDocument_UntrackedDocument_ReturnsFalse()
        {
            // Arrange
            var manager = new DefaultLSPDocumentManager(JoinableTaskContext, UriProvider, LSPDocumentFactory, ChangeListeners);
            manager.TrackDocument(TextBuffer);
            manager.UntrackDocument(TextBuffer);

            // Act
            var result = manager.TryGetDocument(Uri, out var lspDocument);

            // Assert
            Assert.False(result);
            Assert.Null(lspDocument);
        }

        private Lazy<LSPDocumentChangeListener, IContentTypeMetadata> CreateChangeListenerForContentTypes(IEnumerable<string> contentTypes)
        {
            var changeListenerObj = Mock.Of<LSPDocumentChangeListener>(MockBehavior.Strict);

            var metadata = Mock.Of<IContentTypeMetadata>(md =>
                md.ContentTypes == contentTypes,
                MockBehavior.Strict);

            return new Lazy<LSPDocumentChangeListener, IContentTypeMetadata>(() => changeListenerObj, metadata);
        }

        private class TestVirtualDocument : VirtualDocument
        {
            public override Uri Uri => throw new NotImplementedException();

            public override ITextBuffer TextBuffer => throw new NotImplementedException();

            public override VirtualDocumentSnapshot CurrentSnapshot { get; } = new TestVirtualDocumentSnapshot(new Uri("C:/path/to/something.razor.g.cs"), 123);

            [Obsolete]
            public override long? HostDocumentSyncVersion => throw new NotImplementedException();

            public override int HostDocumentVersion => 123;

            [Obsolete]
            public override VirtualDocumentSnapshot Update(IReadOnlyList<ITextChange> changes, long hostDocumentVersion)
            {
                throw new NotImplementedException();
            }

            public override VirtualDocumentSnapshot Update(IReadOnlyList<ITextChange> changes, int hostDocumentVersion)
            {
                return CurrentSnapshot;
            }

            public override void Dispose()
            {
            }
        }
    }
}
