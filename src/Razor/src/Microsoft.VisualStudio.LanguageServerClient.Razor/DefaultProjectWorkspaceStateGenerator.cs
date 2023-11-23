// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Editor.Razor.Logging;

namespace Microsoft.CodeAnalysis.Razor;

[Shared]
[Export(typeof(ProjectWorkspaceStateGenerator))]
[Export(typeof(IProjectSnapshotChangeTrigger))]
[method: ImportingConstructor]
internal class DefaultProjectWorkspaceStateGenerator(ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher, ITelemetryReporter telemetryReporter, IOutputWindowLogger logger) : ProjectWorkspaceStateGenerator, IDisposable
{
    // Internal for testing
    internal readonly Dictionary<ProjectKey, UpdateItem> Updates = new();

    private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher ?? throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter ?? throw new ArgumentNullException(nameof(telemetryReporter));
    private readonly IOutputWindowLogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(initialCount: 1);

    private ProjectSnapshotManagerBase _projectManager;
    private ITagHelperResolver _tagHelperResolver;
    private bool _disposed;

    // Used in unit tests to ensure we can control when background work starts.
    public ManualResetEventSlim BlockBackgroundWorkStart { get; set; }

    // Used in unit tests to ensure we can know when background work finishes.
    public ManualResetEventSlim NotifyBackgroundWorkCompleted { get; set; }

    public override void Initialize(ProjectSnapshotManagerBase projectManager)
    {
        if (projectManager is null)
        {
            throw new ArgumentNullException(nameof(projectManager));
        }

        _projectManager = projectManager;

        _tagHelperResolver = _projectManager.Workspace.Services.GetRequiredService<ITagHelperResolver>();
    }

    public override void Update(Project workspaceProject, IProjectSnapshot projectSnapshot, CancellationToken cancellationToken)
    {
        if (projectSnapshot is null)
        {
            throw new ArgumentNullException(nameof(projectSnapshot));
        }

        _logger.LogDebug("Workspate state update requests for {project}", projectSnapshot.Key);

        _projectSnapshotManagerDispatcher.AssertDispatcherThread();

        if (_disposed)
        {
            return;
        }

        if (Updates.TryGetValue(projectSnapshot.Key, out var updateItem) &&
            !updateItem.Task.IsCompleted &&
            !updateItem.Cts.IsCancellationRequested)
        {
            _logger.LogDebug("Workspate state: Cancelling previous update request for {project}", projectSnapshot.Key);
            updateItem.Cts.Cancel();
        }

        if (updateItem?.Cts.IsCancellationRequested == false)
        {
            _logger.LogDebug("Workspate state: Disposing previous token update request for {project}", projectSnapshot.Key);
            updateItem?.Cts.Dispose();
        }

        _logger.LogDebug("Workspate state: Starting task for update request for {project}", projectSnapshot.Key);
        var lcts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var updateTask = Task.Factory.StartNew(
            () => UpdateWorkspaceStateAsync(workspaceProject, projectSnapshot, lcts.Token),
            lcts.Token,
            TaskCreationOptions.None,
            TaskScheduler.Default).Unwrap();
        updateItem = new UpdateItem(updateTask, lcts);
        Updates[projectSnapshot.Key] = updateItem;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var update in Updates)
        {
            if (!update.Value.Task.IsCompleted &&
                !update.Value.Cts.IsCancellationRequested)
            {
                update.Value.Cts.Cancel();
            }
        }

        // Release before dispose to ensure we don't throw exceptions from the background thread trying to release
        // while we're disposing. Multiple releases are fine, and if we release and it lets something passed the lock
        // our cancellation token check will mean its a no-op.
        _semaphore.Release();
        _semaphore.Dispose();

        BlockBackgroundWorkStart?.Set();
    }

    private async Task UpdateWorkspaceStateAsync(Project workspaceProject, IProjectSnapshot projectSnapshot, CancellationToken cancellationToken)
    {
        // We fire this up on a background thread so we could have been disposed already, and if so, waiting on our semaphore
        // throws an exception.
        if (_disposed)
        {
            return;
        }

        // Specifically not using BeginBlock because we want to capture cases where tag helper discovery never finishes.
        var telemetryId = Guid.NewGuid();
        _telemetryReporter.ReportEvent("taghelperresolve/begin", Severity.Normal,
            new Property("id", telemetryId),
            new Property("tagHelperCount", projectSnapshot.ProjectWorkspaceState?.TagHelpers.Length ?? 0));

        try
        {
            _logger.LogDebug("Workspate state: Waiting on semaphore to update {project}", projectSnapshot.Key);

            // Only allow a single TagHelper resolver request to process at a time in order to reduce Visual Studio memory pressure. Typically a TagHelper resolution result can be upwards of 10mb+.
            // So if we now do multiple requests to resolve TagHelpers simultaneously it results in only a single one executing at a time so that we don't have N number of requests in flight with these
            // 10mb payloads waiting to be processed.
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException or ObjectDisposedException)
            {
                // Object disposed or task cancelled exceptions should be swallowed/no-op'd
                _logger.LogDebug("Workspate state: Cancelled or disposed waiting on semaphore to update {project}", projectSnapshot.Key);
                return;
            }

            _telemetryReporter.ReportFault(ex, "Error waiting on semaphore to update {project}", projectSnapshot.Key);
            return;
        }

        _logger.LogDebug("Workspate state: Got the semaphore to update {project}", projectSnapshot.Key);

        try
        {
            OnStartingBackgroundWork();

            if (cancellationToken.IsCancellationRequested)
            {
                // Silently cancel, we're the only ones creating these tasks.
                _logger.LogDebug("Workspate state: Canceled before we were going to update {project}", projectSnapshot.Key);
                return;
            }

            var workspaceState = ProjectWorkspaceState.Default;
            try
            {
                if (workspaceProject != null)
                {
                    var csharpLanguageVersion = LanguageVersion.Default;
                    var csharpParseOptions = (CSharpParseOptions)workspaceProject.ParseOptions;
                    if (csharpParseOptions is null)
                    {
                        Debug.Fail("Workspace project should always have CSharp parse options.");
                    }
                    else
                    {
                        csharpLanguageVersion = csharpParseOptions.LanguageVersion;
                    }

                    using var _ = StopwatchPool.GetPooledObject(out var watch);

                    _logger.LogDebug("Workspate state: Resolving tag helpers for {project}", projectSnapshot.Key);

                    watch.Restart();
                    var tagHelpers = await _tagHelperResolver.GetTagHelpersAsync(workspaceProject, projectSnapshot, cancellationToken).ConfigureAwait(false);
                    watch.Stop();

                    _telemetryReporter.ReportEvent("taghelperresolve/end", Severity.Normal,
                        new Property("id", telemetryId),
                        new Property("ellapsedms", watch.ElapsedMilliseconds),
                        new Property("result", "success"),
                        new Property("tagHelperCount", tagHelpers.Length));

                    workspaceState = new ProjectWorkspaceState(tagHelpers, csharpLanguageVersion);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Workspate state: Aborting tag helper resolve for {project}", projectSnapshot.Key);

                // Abort work if we get a task cancelled exception
                _telemetryReporter.ReportEvent("taghelperresolve/end", Severity.Normal,
                    new Property("id", telemetryId),
                    new Property("result", "cancel"));

                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Workspate state: Error during tag helper resolve for {project}: {ex}", projectSnapshot.Key, ex);

                _telemetryReporter.ReportEvent("taghelperresolve/end", Severity.Normal,
                    new Property("id", telemetryId),
                    new Property("result", "error"));

                await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                   () => _projectManager.ReportError(ex, projectSnapshot),
                   // Don't allow errors to be cancelled
                   CancellationToken.None).ConfigureAwait(false);
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Workspate state: Cancelled after tag helper resolve for {project}", projectSnapshot.Key);

                // Silently cancel, we're the only ones creating these tasks.
                return;
            }

            await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogDebug("Workspate state: Cancelled after tag helper resolve, before updating {project}", projectSnapshot.Key);

                        return;
                    }

                    _logger.LogDebug("Workspate state: Reporting change for {project}", projectSnapshot.Key);

                    ReportWorkspaceStateChange(projectSnapshot.Key, workspaceState);
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Workspate state: Cancelled while updating {project}", projectSnapshot.Key);

            // Abort work if we get a task canceled exception
            return;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Workspate state: Error while updating {project}: {ex}", projectSnapshot.Key, ex);

            // This is something totally unexpected, let's just send it over to the project manager.
            await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () => _projectManager.ReportError(ex),
                // Don't allow errors to be cancelled
                CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                // Prevent ObjectDisposedException if we've disposed before we got here. The dispose method will release
                // anyway, so we're all good.
                if (!_disposed)
                {
                    _logger.LogDebug("Workspate state: Releasing the semaphore for {project}", projectSnapshot.Key);

                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Workspate state: Error releasing the semaphore to update {project}: {ex}", projectSnapshot.Key, ex);

                // Swallow exceptions that happen from releasing the semaphore.
            }
        }

        OnBackgroundWorkCompleted();

        _logger.LogDebug("Workspate state: Done {project}", projectSnapshot.Key);
    }

    private void ReportWorkspaceStateChange(ProjectKey projectKey, ProjectWorkspaceState workspaceStateChange)
    {
        _projectSnapshotManagerDispatcher.AssertDispatcherThread();

        _projectManager.ProjectWorkspaceStateChanged(projectKey, workspaceStateChange);
    }

    private void OnStartingBackgroundWork()
    {
        if (BlockBackgroundWorkStart != null)
        {
            BlockBackgroundWorkStart.Wait();
            BlockBackgroundWorkStart.Reset();
        }
    }

    private void OnBackgroundWorkCompleted()
    {
        if (NotifyBackgroundWorkCompleted != null)
        {
            NotifyBackgroundWorkCompleted.Set();
        }
    }

    // Internal for testing
    internal class UpdateItem
    {
        public UpdateItem(Task task, CancellationTokenSource cts)
        {
            if (task is null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (cts is null)
            {
                throw new ArgumentNullException(nameof(cts));
            }

            Task = task;
            Cts = cts;
        }

        public Task Task { get; }

        public CancellationTokenSource Cts { get; }
    }
}
