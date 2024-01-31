// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.AspNetCore.Razor.Test.Common.Editor;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Editor.Razor.Settings;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Editor.Razor;

public class RazorDocumentManagerTest : ProjectSnapshotManagerDispatcherTestBase
{
    private const string FilePath = "C:/Some/Path/TestDocumentTracker.cshtml";
    private const string ProjectPath = "C:/Some/Path/TestProject.csproj";

    private static readonly IContentType s_razorCoreContentType =
        Mock.Of<IContentType>(
            c => c.IsOfType(RazorLanguage.CoreContentType) == true,
            MockBehavior.Strict);

    private static readonly IContentType s_nonRazorCoreContentType =
        Mock.Of<IContentType>(
            c => c.IsOfType(It.IsAny<string>()) == false,
            MockBehavior.Strict);

    private readonly IProjectSnapshotManagerAccessor _projectManagerAccessor;
    private readonly IWorkspaceEditorSettings _workspaceEditorSettings;
    private readonly IImportDocumentManager _importDocumentManager;

    public RazorDocumentManagerTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        var projectManagerMock = new Mock<ProjectSnapshotManagerBase>(MockBehavior.Strict);
        projectManagerMock
            .Setup(p => p.GetAllProjectKeys(It.IsAny<string>()))
            .Returns(ImmutableArray<ProjectKey>.Empty);
        projectManagerMock
            .Setup(p => p.GetProjects())
            .Returns(ImmutableArray<IProjectSnapshot>.Empty);

        IProjectSnapshot? projectResult = null;
        projectManagerMock
            .Setup(p => p.TryGetLoadedProject(It.IsAny<ProjectKey>(), out projectResult))
            .Returns(false);

        var projectManager = projectManagerMock.Object;

        var projectManagerAccessorMock = new Mock<IProjectSnapshotManagerAccessor>(MockBehavior.Strict);
        projectManagerAccessorMock
            .SetupGet(x => x.Instance)
            .Returns(projectManager);

        _projectManagerAccessor = projectManagerAccessorMock.Object;

        _workspaceEditorSettings = new WorkspaceEditorSettings(
            Mock.Of<IClientSettingsManager>(MockBehavior.Strict));

        var importDocumentManager = new Mock<IImportDocumentManager>(MockBehavior.Strict);
        importDocumentManager.Setup(m => m.OnSubscribed(It.IsAny<IVisualStudioDocumentTracker>())).Verifiable();
        importDocumentManager.Setup(m => m.OnUnsubscribed(It.IsAny<IVisualStudioDocumentTracker>())).Verifiable();
        _importDocumentManager = importDocumentManager.Object;
    }

    [UIFact]
    public async Task OnTextViewOpened_ForNonRazorTextBuffer_DoesNothing()
    {
        // Arrange
        var editorFactoryService = new Mock<RazorEditorFactoryService>(MockBehavior.Strict);
        var documentManager = new RazorDocumentManager(editorFactoryService.Object, Dispatcher, JoinableTaskContext);
        var textView = Mock.Of<ITextView>(MockBehavior.Strict);
        var buffers = new Collection<ITextBuffer>()
        {
            Mock.Of<ITextBuffer>(b => b.ContentType == s_nonRazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
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
            Mock.Of<ITextBuffer>(b => b.ContentType == s_razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
        };
        var documentTracker = new VisualStudioDocumentTracker(
            Dispatcher, JoinableTaskContext, FilePath, ProjectPath, _projectManagerAccessor, _workspaceEditorSettings,
            ProjectEngineFactories.DefaultProvider, buffers[0], _importDocumentManager) as IVisualStudioDocumentTracker;
        var editorFactoryService = Mock.Of<RazorEditorFactoryService>(
            factoryService => factoryService.TryGetDocumentTracker(
                It.IsAny<ITextBuffer>(), out documentTracker) == true, MockBehavior.Strict);
        var documentManager = new RazorDocumentManager(editorFactoryService, Dispatcher, JoinableTaskContext);

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
            Mock.Of<ITextBuffer>(b => b.ContentType == s_razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
            Mock.Of<ITextBuffer>(b => b.ContentType == s_nonRazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
        };
        var documentTracker = new VisualStudioDocumentTracker(
            Dispatcher, JoinableTaskContext, FilePath, ProjectPath, _projectManagerAccessor, _workspaceEditorSettings, ProjectEngineFactories.DefaultProvider, buffers[0], _importDocumentManager) as IVisualStudioDocumentTracker;
        var editorFactoryService = Mock.Of<RazorEditorFactoryService>(f => f.TryGetDocumentTracker(It.IsAny<ITextBuffer>(), out documentTracker) == true, MockBehavior.Strict);
        var documentManager = new RazorDocumentManager(editorFactoryService, Dispatcher, JoinableTaskContext);

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
        var documentManager = new RazorDocumentManager(Mock.Of<RazorEditorFactoryService>(MockBehavior.Strict), Dispatcher, JoinableTaskContext);
        var textView = Mock.Of<ITextView>(MockBehavior.Strict);
        var buffers = new Collection<ITextBuffer>()
        {
            Mock.Of<ITextBuffer>(b => b.ContentType == s_razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
        };

        // Act
        await documentManager.OnTextViewClosedAsync(textView, buffers);

        // Assert
        Assert.False(buffers[0].Properties.ContainsProperty(typeof(IVisualStudioDocumentTracker)));
    }

    [UIFact]
    public async Task OnTextViewClosed_ForAnyTextBufferWithTracker_RemovesTextView()
    {
        // Arrange
        var textView1 = Mock.Of<ITextView>(MockBehavior.Strict);
        var textView2 = Mock.Of<ITextView>(MockBehavior.Strict);
        var buffers = new Collection<ITextBuffer>()
        {
            Mock.Of<ITextBuffer>(b => b.ContentType == s_razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
            Mock.Of<ITextBuffer>(b => b.ContentType == s_nonRazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
        };

        // Preload the buffer's properties with a tracker, so it's like we've already tracked this one.
        var documentTracker = new VisualStudioDocumentTracker(
            Dispatcher, JoinableTaskContext, FilePath, ProjectPath, _projectManagerAccessor, _workspaceEditorSettings,
            ProjectEngineFactories.DefaultProvider, buffers[0], _importDocumentManager);
        documentTracker.AddTextView(textView1);
        documentTracker.AddTextView(textView2);
        buffers[0].Properties.AddProperty(typeof(IVisualStudioDocumentTracker), documentTracker);

        documentTracker = new VisualStudioDocumentTracker(
            Dispatcher, JoinableTaskContext, FilePath, ProjectPath, _projectManagerAccessor, _workspaceEditorSettings,
            ProjectEngineFactories.DefaultProvider, buffers[1], _importDocumentManager);
        documentTracker.AddTextView(textView1);
        documentTracker.AddTextView(textView2);
        buffers[1].Properties.AddProperty(typeof(IVisualStudioDocumentTracker), documentTracker);

        var editorFactoryService = Mock.Of<RazorEditorFactoryService>(MockBehavior.Strict);
        var documentManager = new RazorDocumentManager(editorFactoryService, Dispatcher, JoinableTaskContext);

        // Act
        await documentManager.OnTextViewClosedAsync(textView2, buffers);

        // Assert
        documentTracker = buffers[0].Properties.GetProperty<VisualStudioDocumentTracker>(typeof(IVisualStudioDocumentTracker));
        Assert.Collection(documentTracker.TextViews, v => Assert.Same(v, textView1));

        documentTracker = buffers[1].Properties.GetProperty<VisualStudioDocumentTracker>(typeof(IVisualStudioDocumentTracker));
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
            Mock.Of<ITextBuffer>(b => b.ContentType == s_razorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
            Mock.Of<ITextBuffer>(b => b.ContentType == s_nonRazorCoreContentType && b.Properties == new PropertyCollection(), MockBehavior.Strict),
        };
        var documentTracker = new VisualStudioDocumentTracker(
            Dispatcher, JoinableTaskContext, FilePath, ProjectPath, _projectManagerAccessor, _workspaceEditorSettings,
            ProjectEngineFactories.DefaultProvider, buffers[0], _importDocumentManager);
        buffers[0].Properties.AddProperty(typeof(IVisualStudioDocumentTracker), documentTracker);
        var editorFactoryService = Mock.Of<RazorEditorFactoryService>(MockBehavior.Strict);
        var documentManager = new RazorDocumentManager(editorFactoryService, Dispatcher, JoinableTaskContext);

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
