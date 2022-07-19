// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class CodeDocumentReferenceHolderTest : LanguageServerTestBase
    {
        public CodeDocumentReferenceHolderTest()
        {
            ProjectManager = TestProjectSnapshotManager.Create(Dispatcher);
            ProjectManager.AllowNotifyListeners = true;
            ReferenceHolder = new CodeDocumentReferenceHolder();
            ReferenceHolder.Initialize(ProjectManager);

            HostProject = new HostProject("C:/path/to/project.csproj", RazorConfiguration.Default, rootNamespace: "TestNamespace");
            HostDocument = new HostDocument("C:/path/to/file.razor", "file.razor");
        }

        private TestProjectSnapshotManager ProjectManager { get; }

        private CodeDocumentReferenceHolder ReferenceHolder { get; }

        private HostProject HostProject { get; }

        private HostDocument HostDocument { get; }

        [Fact]
        public async Task DocumentProcessed_ReferencesGeneratedCodeDocument()
        {
            // Arrange
            var documentSnapshot = await CreateDocumentSnapshotAsync(CancellationToken.None).ConfigureAwait(false);
            var codeDocumentReference = await ProcessDocumentAndRetrieveOutputAsync(documentSnapshot, CancellationToken.None).ConfigureAwait(false);

            // Act
            GC.Collect();

            // Assert
            Assert.True(codeDocumentReference.TryGetTarget(out _));
        }

        [Fact]
        public async Task UnrelatedDocumentChanged_ReferencesGeneratedCodeDocument()
        {
            // Arrange
            var documentSnapshot = await CreateDocumentSnapshotAsync(CancellationToken.None).ConfigureAwait(false);
            var unrelatedHostDocument = new HostDocument("C:/path/to/otherfile.razor", "otherfile.razor");
            var unrelatedDocumentSnapshot = await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                var unrelatedTextLoader = new SourceTextLoader("<p>Unrelated</p>", unrelatedHostDocument.FilePath);
                ProjectManager.DocumentAdded(HostProject, unrelatedHostDocument, unrelatedTextLoader);
                var project = ProjectManager.GetLoadedProject(HostProject.FilePath);
                var document = project.GetDocument(unrelatedHostDocument.FilePath);
                return document;
            }, CancellationToken.None).ConfigureAwait(false);

            var mainCodeDocumentReference = await ProcessDocumentAndRetrieveOutputAsync(documentSnapshot, CancellationToken.None).ConfigureAwait(false);
            var unrelatedCodeDocumentReference = await ProcessDocumentAndRetrieveOutputAsync(unrelatedDocumentSnapshot, CancellationToken.None).ConfigureAwait(false);

            // Act
            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                ProjectManager.DocumentChanged(HostProject.FilePath, unrelatedHostDocument.FilePath, SourceText.From(string.Empty));
            }, CancellationToken.None).ConfigureAwait(false);

            GC.Collect();

            // Assert
            Assert.True(mainCodeDocumentReference.TryGetTarget(out _));
            Assert.False(unrelatedCodeDocumentReference.TryGetTarget(out _));
        }

        [Fact]
        public async Task DocumentChanged_DereferencesGeneratedCodeDocument()
        {
            // Arrange
            var documentSnapshot = await CreateDocumentSnapshotAsync(CancellationToken.None).ConfigureAwait(false);
            var codeDocumentReference = await ProcessDocumentAndRetrieveOutputAsync(documentSnapshot, CancellationToken.None).ConfigureAwait(false);

            // Act
            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                ProjectManager.DocumentChanged(HostProject.FilePath, HostDocument.FilePath, SourceText.From(string.Empty));
            }, CancellationToken.None).ConfigureAwait(false);

            GC.Collect();

            // Assert
            Assert.False(codeDocumentReference.TryGetTarget(out _));
        }

        [Fact]
        public async Task DocumentRemoved_DereferencesGeneratedCodeDocument()
        {
            // Arrange
            var documentSnapshot = await CreateDocumentSnapshotAsync(CancellationToken.None).ConfigureAwait(false);
            var codeDocumentReference = await ProcessDocumentAndRetrieveOutputAsync(documentSnapshot, CancellationToken.None).ConfigureAwait(false);

            // Act
            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                ProjectManager.DocumentRemoved(HostProject, HostDocument);
            }, CancellationToken.None).ConfigureAwait(false);

            GC.Collect();

            // Assert
            Assert.False(codeDocumentReference.TryGetTarget(out _));
        }

        [Fact]
        public async Task ProjectChanged_DereferencesGeneratedCodeDocument()
        {
            // Arrange
            var documentSnapshot = await CreateDocumentSnapshotAsync(CancellationToken.None).ConfigureAwait(false);
            var codeDocumentReference = await ProcessDocumentAndRetrieveOutputAsync(documentSnapshot, CancellationToken.None).ConfigureAwait(false);

            // Act
            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                ProjectManager.ProjectConfigurationChanged(new HostProject(HostProject.FilePath, RazorConfiguration.Default, rootNamespace: "NewRootNamespace"));
            }, CancellationToken.None).ConfigureAwait(false);

            GC.Collect();

            // Assert
            Assert.False(codeDocumentReference.TryGetTarget(out _));
        }

        [Fact]
        public async Task ProjectRemoved_DereferencesGeneratedCodeDocument()
        {
            // Arrange
            var documentSnapshot = await CreateDocumentSnapshotAsync(CancellationToken.None).ConfigureAwait(false);
            var codeDocumentReference = await ProcessDocumentAndRetrieveOutputAsync(documentSnapshot, CancellationToken.None).ConfigureAwait(false);

            // Act
            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                ProjectManager.ProjectRemoved(HostProject);
            }, CancellationToken.None).ConfigureAwait(false);

            GC.Collect();

            // Assert
            Assert.False(codeDocumentReference.TryGetTarget(out _));
        }

        private Task<DocumentSnapshot> CreateDocumentSnapshotAsync(CancellationToken cancellationToken)
        {
            return Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                ProjectManager.ProjectAdded(HostProject);
                var textLoader = new SourceTextLoader("<p>Hello World</p>", HostDocument.FilePath);
                ProjectManager.DocumentAdded(HostProject, HostDocument, textLoader);
                var project = ProjectManager.GetLoadedProject(HostProject.FilePath);
                var document = project.GetDocument(HostDocument.FilePath);
                return document;
            }, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async Task<WeakReference<RazorCodeDocument>> ProcessDocumentAndRetrieveOutputAsync(DocumentSnapshot documentSnapshot, CancellationToken cancellationToken)
        {
            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            await Dispatcher.RunOnDispatcherThreadAsync(() =>
            {
                ReferenceHolder.DocumentProcessed(codeDocument, documentSnapshot);
            }, cancellationToken).ConfigureAwait(false);
            var codeDocumentReference = new WeakReference<RazorCodeDocument>(codeDocument);

            return codeDocumentReference;
        }

        private sealed class SourceTextLoader : TextLoader
        {
            private readonly SourceText _sourceText;
            private readonly string _filePath;

            public SourceTextLoader(string content, string filePath)
            {
                _sourceText = SourceText.From(content);
                _filePath = filePath;
            }

            public override Task<TextAndVersion> LoadTextAndVersionAsync(Workspace workspace, DocumentId documentId, CancellationToken cancellationToken)
            {
                return Task.FromResult(TextAndVersion.Create(_sourceText, VersionStamp.Default, _filePath));
            }
        }
    }
}
