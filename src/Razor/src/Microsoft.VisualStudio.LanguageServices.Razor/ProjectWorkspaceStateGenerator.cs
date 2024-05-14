﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(IProjectWorkspaceStateGenerator))]
[method: ImportingConstructor]
internal sealed partial class ProjectWorkspaceStateGenerator(
    IProjectSnapshotManager projectManager,
    ITagHelperResolver tagHelperResolver,
    ILoggerFactory loggerFactory,
    ITelemetryReporter telemetryReporter)
    : IProjectWorkspaceStateGenerator, IDisposable
{
    private readonly IProjectSnapshotManager _projectManager = projectManager;
    private readonly ITagHelperResolver _tagHelperResolver = tagHelperResolver;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<ProjectWorkspaceStateGenerator>();
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;

    private readonly SemaphoreSlim _semaphore = new(initialCount: 1);
    private bool _disposed;

    private readonly Dictionary<ProjectKey, UpdateItem> _updates = [];

    private ManualResetEventSlim? _blockBackgroundWorkStart;
    private ManualResetEventSlim? _notifyBackgroundWorkCompleted;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // We mark ourselves as disposed here to ensure that no further updates can be enqueued
        // before we cancel the updates.
        _disposed = true;

        CancelUpdates();

        // Release before dispose to ensure we don't throw exceptions from the background thread trying to release
        // while we're disposing. Multiple releases are fine, and if we release and it lets something passed the lock
        // our cancellation token check will mean its a no-op.
        _logger.LogTrace($"Releasing the semaphore in Dispose");
        _semaphore.Release();
        _semaphore.Dispose();

        _blockBackgroundWorkStart?.Set();
    }

    public void EnqueueUpdate(Project? workspaceProject, IProjectSnapshot projectSnapshot)
    {
        if (_disposed)
        {
            return;
        }

        lock (_updates)
        {
            var projectKey = projectSnapshot.Key;

            if (_updates.TryGetValue(projectKey, out var updateItem))
            {
                if (updateItem.IsRunning)
                {
                    _logger.LogTrace($"Cancelling previously enqueued update for '{projectKey}'.");
                }

                updateItem.CancelWorkAndCleanUp();
            }

            _logger.LogTrace($"Enqueuing update for '{projectKey}'");

            _updates[projectKey] = UpdateItem.CreateAndStartWork(
                token => UpdateWorkspaceStateAsync(workspaceProject, projectSnapshot, token));
        }
    }

    public void CancelUpdates()
    {
        lock (_updates)
        {
            if (_updates.Count == 0)
            {
                return;
            }

            _logger.LogTrace($"Cancelling all previously enqueued updates.");

            foreach (var (_, updateItem) in _updates)
            {
                updateItem.CancelWorkAndCleanUp();
            }

            _updates.Clear();
        }
    }

    private async Task UpdateWorkspaceStateAsync(Project? workspaceProject, IProjectSnapshot projectSnapshot, CancellationToken cancellationToken)
    {
        var projectKey = projectSnapshot.Key;

        // Only allow a single TagHelper resolver request to process at a time in order to reduce
        // Visual Studio memory pressure. Typically a TagHelper resolution result can be upwards of 10mb+.
        // So if we now do multiple requests to resolve TagHelpers simultaneously it results in only a
        // single one executing at a time so that we don't have N number of requests in flight with these
        // 10mb payloads waiting to be processed.

        var enteredSemaphore = await TryEnterSemaphoreAsync(projectKey, cancellationToken);
        if (!enteredSemaphore)
        {
            return;
        }

        try
        {
            OnStartingBackgroundWork();

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var workspaceState = await GetProjectWorkspaceStateAsync(workspaceProject, projectSnapshot, cancellationToken);

            if (workspaceState is null)
            {
                _logger.LogTrace($"Didn't receive {nameof(ProjectWorkspaceState)} for '{projectKey}'");
                return;
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogTrace($"Got a cancellation request during discovery for '{projectKey}'");
                return;
            }

            _logger.LogTrace($"Received {nameof(ProjectWorkspaceState)} with {workspaceState.TagHelpers.Length} tag helper(s) for '{projectKey}'");

            await _projectManager
                .UpdateAsync(
                    static (updater, state) =>
                    {
                        var (projectKey, workspaceState, logger, cancellationToken) = state;

                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        logger.LogTrace($"Updating project with {workspaceState.TagHelpers.Length} tag helper(s) for '{projectKey}'");
                        updater.ProjectWorkspaceStateChanged(projectKey, workspaceState);
                    },
                    state: (projectKey, workspaceState, _logger, cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogTrace($"Got an OperationCancelledException, for '{projectKey}'");
            // Abort work if we get a task canceled exception
            return;
        }
        catch (Exception ex)
        {
            _logger.LogTrace($"Got an exception, for '{projectKey}'");
            _logger.LogError(ex);
        }
        finally
        {
            ReleaseSemaphore(projectKey);
        }

        _logger.LogTrace($"All finished for '{projectKey}'");

        OnBackgroundWorkCompleted();
    }

    /// <summary>
    /// Attempts to enter the semaphore and returns <see langword="false"/> on failure.
    /// </summary>
    private async Task<bool> TryEnterSemaphoreAsync(ProjectKey projectKey, CancellationToken cancellationToken)
    {
        _logger.LogTrace($"Try to enter semaphore for '{projectKey}'");

        if (_disposed)
        {
            _logger.LogTrace($"Cannot enter semaphore because we have been disposed.");
            return false;
        }

        try
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogTrace($"Entered semaphore for '{projectKey}'");
            return true;
        }
        catch (Exception ex)
        {
            // Swallow object and task cancelled exceptions
            _logger.LogTrace($"""
                Exception occurred while entering semaphore for '{projectKey}':
                {ex}
                """);
            return false;
        }
    }

    private void ReleaseSemaphore(ProjectKey projectKey)
    {
        try
        {
            // Prevent ObjectDisposedException if we've disposed before we got here.
            // The dispose method will release anyway, so we're all good.
            if (_disposed)
            {
                return;
            }

            _semaphore.Release();
            _logger.LogTrace($"Released semaphore for '{projectKey}'");
        }
        catch (Exception ex)
        {
            // Swallow object and task cancelled exceptions
            _logger.LogTrace($"""
                Exception occurred while releasing semaphore for '{projectKey}':
                {ex}
                """);
        }
    }

    /// <summary>
    ///  Attempts to produce a <see cref="ProjectWorkspaceState"/> from the provide <see cref="Project"/> and <see cref="IProjectSnapshot"/>.
    ///  Returns <see langword="null"/> if an error is encountered.
    /// </summary>
    private async Task<ProjectWorkspaceState?> GetProjectWorkspaceStateAsync(
        Project? workspaceProject,
        IProjectSnapshot projectSnapshot,
        CancellationToken cancellationToken)
    {
        // This is the simplest case. If we don't have a project (likely because it is being removed),
        // we just a default ProjectWorkspaceState.
        if (workspaceProject is null)
        {
            return ProjectWorkspaceState.Default;
        }

        _logger.LogTrace($"Starting tag helper discovery for {projectSnapshot.FilePath}");

        // Specifically not using BeginBlock because we want to capture cases where tag helper discovery never finishes.
        var telemetryId = Guid.NewGuid();

        _telemetryReporter.ReportEvent("taghelperresolve/begin", Severity.Normal,
            new("id", telemetryId),
            new("tagHelperCount", projectSnapshot.ProjectWorkspaceState.TagHelpers.Length));

        try
        {
            var csharpLanguageVersion = workspaceProject.ParseOptions is CSharpParseOptions csharpParseOptions
                ? csharpParseOptions.LanguageVersion
                : LanguageVersion.Default;

            using var _ = StopwatchPool.GetPooledObject(out var watch);

            watch.Restart();
            var tagHelpers = await _tagHelperResolver
                .GetTagHelpersAsync(workspaceProject, projectSnapshot, cancellationToken)
                .ConfigureAwait(false);
            watch.Stop();

            // don't report success if the work was cancelled
            cancellationToken.ThrowIfCancellationRequested();

            _telemetryReporter.ReportEvent("taghelperresolve/end", Severity.Normal,
                new("id", telemetryId),
                new("ellapsedms", watch.ElapsedMilliseconds),
                new("result", "success"),
                new("tagHelperCount", tagHelpers.Length));

            _logger.LogInformation($"""
                Resolved tag helpers for project in {watch.ElapsedMilliseconds} ms.
                Project: {projectSnapshot.FilePath}
                """);

            return ProjectWorkspaceState.Create(tagHelpers, csharpLanguageVersion);
        }
        catch (OperationCanceledException)
        {
            // Abort work if we get a task cancelled exception
            _telemetryReporter.ReportEvent("taghelperresolve/end", Severity.Normal,
                new("id", telemetryId),
                new("result", "cancel"));
        }
        catch (Exception ex)
        {
            _telemetryReporter.ReportEvent("taghelperresolve/end", Severity.Normal,
                new("id", telemetryId),
                new("result", "error"));

            _logger.LogError(ex, $"""
                Exception thrown during tag helper resolution for project.
                Project: {projectSnapshot.FilePath}
                """);
        }

        return null;
    }

    private void OnStartingBackgroundWork()
    {
        if (_blockBackgroundWorkStart is { } resetEvent)
        {
            resetEvent.Wait();
            resetEvent.Reset();
        }
    }

    private void OnBackgroundWorkCompleted()
    {
        if (_notifyBackgroundWorkCompleted is { } resetEvent)
        {
            resetEvent.Set();
        }
    }
}
