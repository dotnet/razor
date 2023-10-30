// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

public class OpenDocumentGeneratorTest(ITestOutputHelper testOutput) : LanguageServerTestBase(testOutput)
{
    private readonly HostDocument[] _documents =
    [
        new HostDocument("c:/Test1/Index.cshtml", "Index.cshtml"),
        new HostDocument("c:/Test1/Components/Counter.cshtml", "Components/Counter.cshtml"),
    ];

    private readonly HostProject _hostProject1 = new("c:/Test1/Test1.csproj", "c:/Test1/obj", RazorConfiguration.Default, "TestRootNamespace");
    private readonly HostProject _hostProject2 = new("c:/Test2/Test2.csproj", "c:/Test2/obj", RazorConfiguration.Default, "TestRootNamespace");

    [Fact]
    public async Task DocumentAdded_IgnoresClosedDocument()
    {
        // Arrange
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var listener = new TestDocumentProcessedListener();
        using var queue = new TestOpenDocumentGenerator(Dispatcher, ErrorReporter, listener);

        await SwitchToDispatcherThreadAsync();

        projectManager.ProjectAdded(_hostProject1);
        projectManager.ProjectAdded(_hostProject2);
        projectManager.AllowNotifyListeners = true;

        queue.Initialize(projectManager);

        // Act
        projectManager.DocumentAdded(_hostProject1.Key, _documents[0], null!);

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => listener.GetProcessedDocumentAsync(cancelAfter: TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task DocumentChanged_IgnoresClosedDocument()
    {
        // Arrange
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var listener = new TestDocumentProcessedListener();
        using var queue = new TestOpenDocumentGenerator(Dispatcher, ErrorReporter, listener);

        await SwitchToDispatcherThreadAsync();

        projectManager.ProjectAdded(_hostProject1);
        projectManager.ProjectAdded(_hostProject2);
        projectManager.AllowNotifyListeners = true;
        projectManager.DocumentAdded(_hostProject1.Key, _documents[0], null!);

        queue.Initialize(projectManager);

        // Act
        projectManager.DocumentChanged(_hostProject1.Key, _documents[0].FilePath, SourceText.From("new"));

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => listener.GetProcessedDocumentAsync(cancelAfter: TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task DocumentChanged_ProcessesOpenDocument()
    {
        // Arrange
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var listener = new TestDocumentProcessedListener();
        using var queue = new TestOpenDocumentGenerator(Dispatcher, ErrorReporter, listener);

        await SwitchToDispatcherThreadAsync();

        projectManager.ProjectAdded(_hostProject1);
        projectManager.ProjectAdded(_hostProject2);
        projectManager.AllowNotifyListeners = true;
        projectManager.DocumentAdded(_hostProject1.Key, _documents[0], null!);
        projectManager.DocumentOpened(_hostProject1.Key, _documents[0].FilePath, SourceText.From(string.Empty));

        queue.Initialize(projectManager);

        // Act
        projectManager.DocumentChanged(_hostProject1.Key, _documents[0].FilePath, SourceText.From("new"));

        // Assert

        var document = await listener.GetProcessedDocumentAsync(cancelAfter: TimeSpan.FromSeconds(10));
        Assert.Equal(document.FilePath, _documents[0].FilePath);
    }

    [Fact]
    public async Task ProjectChanged_IgnoresClosedDocument()
    {
        // Arrange
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var listener = new TestDocumentProcessedListener();
        using var queue = new TestOpenDocumentGenerator(Dispatcher, ErrorReporter, listener);

        await SwitchToDispatcherThreadAsync();

        projectManager.ProjectAdded(_hostProject1);
        projectManager.ProjectAdded(_hostProject2);
        projectManager.AllowNotifyListeners = true;
        projectManager.DocumentAdded(_hostProject1.Key, _documents[0], null!);

        queue.Initialize(projectManager);

        // Act
        projectManager.ProjectWorkspaceStateChanged(_hostProject1.Key,
            new ProjectWorkspaceState(ImmutableArray<TagHelperDescriptor>.Empty, LanguageVersion.CSharp8));

        // Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() => listener.GetProcessedDocumentAsync(cancelAfter: TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task ProjectChanged_ProcessesOpenDocument()
    {
        // Arrange
        var projectManager = TestProjectSnapshotManager.Create(ErrorReporter, Dispatcher);
        var listener = new TestDocumentProcessedListener();
        using var queue = new TestOpenDocumentGenerator(Dispatcher, ErrorReporter, listener);

        await SwitchToDispatcherThreadAsync();

        projectManager.ProjectAdded(_hostProject1);
        projectManager.ProjectAdded(_hostProject2);
        projectManager.AllowNotifyListeners = true;
        projectManager.DocumentAdded(_hostProject1.Key, _documents[0], null!);
        projectManager.DocumentOpened(_hostProject1.Key, _documents[0].FilePath, SourceText.From(string.Empty));

        queue.Initialize(projectManager);

        // Act
        projectManager.ProjectWorkspaceStateChanged(_hostProject1.Key,
            new ProjectWorkspaceState(ImmutableArray<TagHelperDescriptor>.Empty, LanguageVersion.CSharp8));

        // Assert

        var document = await listener.GetProcessedDocumentAsync(cancelAfter: TimeSpan.FromSeconds(10));
        Assert.Equal(document.FilePath, _documents[0].FilePath);
    }

    private class TestOpenDocumentGenerator(ProjectSnapshotManagerDispatcher dispatcher, IErrorReporter errorReporter, params DocumentProcessedListener[] listeners)
        : OpenDocumentGenerator(listeners, dispatcher, TestLanguageServerFeatureOptions.Instance, errorReporter)
    {
    }

    private class TestDocumentProcessedListener : DocumentProcessedListener
    {
        private readonly TaskCompletionSource<IDocumentSnapshot> _tcs;

        public TestDocumentProcessedListener()
        {
            _tcs = new TaskCompletionSource<IDocumentSnapshot>();
        }

        public Task<IDocumentSnapshot> GetProcessedDocumentAsync(TimeSpan cancelAfter)
        {
            var cts = new CancellationTokenSource(cancelAfter);
            var registration = cts.Token.Register(() => _tcs.SetCanceled(cts.Token));
            _ = _tcs.Task.ContinueWith(
                (t) =>
                {
                    registration.Dispose();
                    cts.Dispose();
                },
                TaskScheduler.Current);

            return _tcs.Task;
        }

        public override ValueTask DocumentProcessedAsync(RazorCodeDocument codeDocument, IDocumentSnapshot document, CancellationToken cancellationToken)
        {
            _tcs.SetResult(document);
            return ValueTask.CompletedTask;
        }

        public override void Initialize(ProjectSnapshotManager projectManager)
        {
        }
    }
}
