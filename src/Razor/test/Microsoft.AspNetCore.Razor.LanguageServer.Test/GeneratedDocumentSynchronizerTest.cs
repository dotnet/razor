// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class GeneratedDocumentSynchronizerTest : LanguageServerTestBase
    {
        public GeneratedDocumentSynchronizerTest()
        {
            Cache = new DefaultDocumentVersionCache(Dispatcher);
            Publisher = new TestGeneratedDocumentPublisher();
            Synchronizer = new GeneratedDocumentSynchronizer(Publisher, Cache, Dispatcher);
            Document = TestDocumentSnapshot.Create("C:/path/to/file.razor");
            CodeDocument = CreateCodeDocument("<p>Hello World</p>");
        }

        private DefaultDocumentVersionCache Cache { get; }

        private GeneratedDocumentSynchronizer Synchronizer { get; }

        private TestGeneratedDocumentPublisher Publisher { get; }

        private DocumentSnapshot Document { get; }

        private RazorCodeDocument CodeDocument { get; }

        [Fact]
        public async Task DocumentProcessed_UnknownVersion_Noops()
        {
            // Arrange

            // Act
            await Dispatcher.RunOnDispatcherThreadAsync(() => Synchronizer.DocumentProcessed(CodeDocument, Document), CancellationToken.None);

            // Assert
            Assert.False(Publisher.PublishedCSharp);
            Assert.False(Publisher.PublishedHtml);
        }

        [Fact]
        public async Task DocumentProcessed_KnownVersion_Publishes()
        {
            // Arrange
            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                Cache.TrackDocumentVersion(Document, version: 1337);

                // Act
                Synchronizer.DocumentProcessed(CodeDocument, Document);
            }, CancellationToken.None);

            // Assert
            Assert.True(Publisher.PublishedCSharp);
            Assert.True(Publisher.PublishedHtml);
        }

        private class TestGeneratedDocumentPublisher : GeneratedDocumentPublisher
        {
            public override void Initialize(ProjectSnapshotManagerBase projectManager)
            {
            }

            public bool PublishedCSharp { get; private set; }

            public bool PublishedHtml { get; private set; }

            public override void PublishCSharp(string filePath, SourceText sourceText, int hostDocumentVersion)
            {
                PublishedCSharp = true;
            }

            public override void PublishHtml(string filePath, SourceText sourceText, int hostDocumentVersion)
            {
                PublishedHtml = true;
            }
        }
    }
}
