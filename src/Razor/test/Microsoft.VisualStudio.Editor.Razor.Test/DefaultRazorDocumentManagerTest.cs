// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Editor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.Editor.Razor
{
    public class DefaultRazorDocumentManagerTest : ProjectSnapshotManagerDispatcherTestBase
    {
        private JoinableTaskContext JoinableTaskContext => JoinableTaskFactory.Context;

        private IContentType RazorCoreContentType { get; } = Mock.Of<IContentType>(c => c.IsOfType(RazorLanguage.CoreContentType) == true, MockBehavior.Strict);

        private IContentType NonRazorCoreContentType { get; } = Mock.Of<IContentType>(c => c.IsOfType(It.IsAny<string>()) == false, MockBehavior.Strict);

        private static string FilePath => "C:/Some/Path/TestDocumentTracker.cshtml";

        private static string ProjectPath => "C:/Some/Path/TestProject.csproj";

        private ProjectSnapshotManager ProjectManager => Mock.Of<ProjectSnapshotManager>(p => p.Projects == new List<ProjectSnapshot>() && p.GetOrCreateProject(It.IsAny<string>()) == null, MockBehavior.Strict);

        private WorkspaceEditorSettings WorkspaceEditorSettings => new DefaultWorkspaceEditorSettings(Dispatcher, Mock.Of<EditorSettingsManager>(MockBehavior.Strict));

        private ImportDocumentManager ImportDocumentManager
        {
            get
            {
                var importDocumentManager = new Mock<ImportDocumentManager>(MockBehavior.Strict);
                importDocumentManager.Setup(m => m.OnSubscribed(It.IsAny<VisualStudioDocumentTracker>())).Verifiable();
                importDocumentManager.Setup(m => m.OnUnsubscribed(It.IsAny<VisualStudioDocumentTracker>())).Verifiable();
                return importDocumentManager.Object;
            }
        }

        private static Workspace Workspace => TestWorkspace.Create();

        [UIFact]
        public async Task OnTextViewOpened_ForNonRazorTextBuffer_DoesNothing()
        {
            // Arrange
            var editorFactoryService = new Mock<RazorEditorFactoryService>(MockBehavior.Strict);
            var documentManager = new DefaultRazorDocumentManager(Dispatcher, JoinableTaskContext, editorFactoryService.Object);
            var textView = Mock.Of<ITextView>(MockBehavior.Strict);
            var buffers = new Collection<ITextBuffer>()
            {
                Mock.Of<ITextBuffer>(b => b.ContentType == NonRazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
            };

            // Act & Assert
            await documentManager.OnTextViewOpenedAsync(textView, buffers);
        }

        [UIFact]
        public async Task OnTextViewOpened_ForRazorTextBuffer_AddsTextViewToTracker()
        {
            // Arrange
            var textView = Mock.Of<ITextView>(MockBehavior.Strict);
            var buffers = new Collection<ITextBuffer>()
            {
                Mock.Of<ITextBuffer>(b => b.ContentType == RazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
            };
            var documentTracker = new DefaultVisualStudioDocumentTracker(
                Dispatcher, JoinableTaskContext, FilePath, ProjectPath, ProjectManager, WorkspaceEditorSettings,
                Workspace, buffers[0], ImportDocumentManager) as VisualStudioDocumentTracker;
            var editorFactoryService = Mock.Of<RazorEditorFactoryService>(
                factoryService => factoryService.TryGetDocumentTracker(
                    It.IsAny<ITextBuffer>(), out documentTracker) == true, MockBehavior.Strict);
            var documentManager = new DefaultRazorDocumentManager(Dispatcher, JoinableTaskContext, editorFactoryService);

            // Act
            await documentManager.OnTextViewOpenedAsync(textView, buffers);

            // Assert
            Assert.Collection(documentTracker.TextViews, v => Assert.Same(v, textView));
        }

        [UIFact]
        public async Task OnTextViewOpened_SubscribesAfterFirstTextViewOpened()
        {
            // Arrange
            var textView = Mock.Of<ITextView>(MockBehavior.Strict);
            var buffers = new Collection<ITextBuffer>()
            {
                Mock.Of<ITextBuffer>(b => b.ContentType == RazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
                Mock.Of<ITextBuffer>(b => b.ContentType == NonRazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
            };
            var documentTracker = new DefaultVisualStudioDocumentTracker(
                Dispatcher, JoinableTaskContext, FilePath, ProjectPath, ProjectManager, WorkspaceEditorSettings, Workspace, buffers[0], ImportDocumentManager) as VisualStudioDocumentTracker;
            var editorFactoryService = Mock.Of<RazorEditorFactoryService>(f => f.TryGetDocumentTracker(It.IsAny<ITextBuffer>(), out documentTracker) == true, MockBehavior.Strict);
            var documentManager = new DefaultRazorDocumentManager(Dispatcher, JoinableTaskContext, editorFactoryService);

            // Assert 1
            Assert.False(documentTracker.IsSupportedProject);

            // Act
            await documentManager.OnTextViewOpenedAsync(textView, buffers);

            // Assert 2
            Assert.True(documentTracker.IsSupportedProject);
        }

        [UIFact]
        public async Task OnTextViewClosed_TextViewWithoutDocumentTracker_DoesNothing()
        {
            // Arrange
            var documentManager = new DefaultRazorDocumentManager(Dispatcher, JoinableTaskContext, Mock.Of<RazorEditorFactoryService>(MockBehavior.Strict));
            var textView = Mock.Of<ITextView>(MockBehavior.Strict);
            var buffers = new Collection<ITextBuffer>()
            {
                Mock.Of<ITextBuffer>(b => b.ContentType == RazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
            };

            // Act
            await documentManager.OnTextViewClosedAsync(textView, buffers);

            // Assert
            Assert.False(buffers[0].Properties.ContainsProperty(typeof(VisualStudioDocumentTracker)));
        }

        [UIFact]
        public async Task OnTextViewClosed_ForAnyTextBufferWithTracker_RemovesTextView()
        {
            // Arrange
            var textView1 = Mock.Of<ITextView>(MockBehavior.Strict);
            var textView2 = Mock.Of<ITextView>(MockBehavior.Strict);
            var buffers = new Collection<ITextBuffer>()
            {
                Mock.Of<ITextBuffer>(b => b.ContentType == RazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
                Mock.Of<ITextBuffer>(b => b.ContentType == NonRazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
            };

            // Preload the buffer's properties with a tracker, so it's like we've already tracked this one.
            var documentTracker = new DefaultVisualStudioDocumentTracker(
                Dispatcher, JoinableTaskContext, FilePath, ProjectPath, ProjectManager, WorkspaceEditorSettings,
                Workspace, buffers[0], ImportDocumentManager);
            documentTracker.AddTextView(textView1);
            documentTracker.AddTextView(textView2);
            buffers[0].Properties.AddProperty(typeof(VisualStudioDocumentTracker), documentTracker);

            documentTracker = new DefaultVisualStudioDocumentTracker(
                Dispatcher, JoinableTaskContext, FilePath, ProjectPath, ProjectManager, WorkspaceEditorSettings,
                Workspace, buffers[1], ImportDocumentManager);
            documentTracker.AddTextView(textView1);
            documentTracker.AddTextView(textView2);
            buffers[1].Properties.AddProperty(typeof(VisualStudioDocumentTracker), documentTracker);

            var editorFactoryService = Mock.Of<RazorEditorFactoryService>(MockBehavior.Strict);
            var documentManager = new DefaultRazorDocumentManager(Dispatcher, JoinableTaskContext, editorFactoryService);

            // Act
            await documentManager.OnTextViewClosedAsync(textView2, buffers);

            // Assert
            documentTracker = buffers[0].Properties.GetProperty<DefaultVisualStudioDocumentTracker>(typeof(VisualStudioDocumentTracker));
            Assert.Collection(documentTracker.TextViews, v => Assert.Same(v, textView1));

            documentTracker = buffers[1].Properties.GetProperty<DefaultVisualStudioDocumentTracker>(typeof(VisualStudioDocumentTracker));
            Assert.Collection(documentTracker.TextViews, v => Assert.Same(v, textView1));
        }

        [UIFact]
        public async Task OnTextViewClosed_UnsubscribesAfterLastTextViewClosed()
        {
            // Arrange
            var textView1 = Mock.Of<ITextView>(MockBehavior.Strict);
            var textView2 = Mock.Of<ITextView>(MockBehavior.Strict);
            var buffers = new Collection<ITextBuffer>()
            {
                Mock.Of<ITextBuffer>(b => b.ContentType == RazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
                Mock.Of<ITextBuffer>(b => b.ContentType == NonRazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
            };
            var documentTracker = new DefaultVisualStudioDocumentTracker(
                Dispatcher, JoinableTaskContext, FilePath, ProjectPath, ProjectManager, WorkspaceEditorSettings,
                Workspace, buffers[0], ImportDocumentManager);
            buffers[0].Properties.AddProperty(typeof(VisualStudioDocumentTracker), documentTracker);
            var editorFactoryService = Mock.Of<RazorEditorFactoryService>(MockBehavior.Strict);
            var documentManager = new DefaultRazorDocumentManager(Dispatcher, JoinableTaskContext, editorFactoryService);

            // Populate the text views
            documentTracker.Subscribe();
            documentTracker.AddTextView(textView1);
            documentTracker.AddTextView(textView2);

            // Act 1
            await documentManager.OnTextViewClosedAsync(textView2, buffers);

            // Assert 1
            Assert.True(documentTracker.IsSupportedProject);

            // Act
            await documentManager.OnTextViewClosedAsync(textView1, buffers);

            // Assert 2
            Assert.False(documentTracker.IsSupportedProject);
        }
    }
}
