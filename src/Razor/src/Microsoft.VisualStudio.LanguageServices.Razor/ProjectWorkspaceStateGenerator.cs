// Copyright (c) .NET Foundation. All rights reserved.
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
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Razor;

[Export(typeof(IProjectWorkspaceStateGenerator))]
[method: ImportingConstructor]
internal sealed partial class ProjectWorkspaceStateGenerator(
    IProjectSnapshotManager projectManager,
    ITagHelperResolver tagHelperResolver,
    IErrorReporter errorReporter,
    ITelemetryReporter telemetryReporter)
    : IProjectWorkspaceStateGenerator, IDisposable
{
    private readonly Dictionary<ProjectKey, UpdateItem> _updates = [];

    private readonly IProjectSnapshotManager _projectManager = projectManager;
    private readonly ITagHelperResolver _tagHelperResolver = tagHelperResolver;
    private readonly IErrorReporter _errorReporter = errorReporter;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;
    private readonly SemaphoreSlim _semaphore = new(initialCount: 1);

    private readonly CancellationTokenSource _disposeTokenSource = new();

    private ManualResetEventSlim? _blockBackgroundWorkStart;
    private ManualResetEventSlim? _notifyBackgroundWorkCompleted;

    public void EnqueueUpdate(Project? workspaceProject, IProjectSnapshot projectSnapshot)
    {
        if (_disposeTokenSource.IsCancellationRequested)
        {
            return;
        }

        lock (_updates)
        {
            if (_updates.TryGetValue(projectSnapshot.Key, out var updateItem))
            {
                updateItem.Dispose();
            }

            _updates[projectSnapshot.Key] = new UpdateItem(
                token => UpdateWorkspaceStateAsync(workspaceProject, projectSnapshot, token),
                _disposeTokenSource.Token);
        }
    }

    public void CancelUpdates()
    {
        lock (_updates)
        {
            foreach (var (_, updateItem) in _updates)
            {
                updateItem.Dispose();
            }

            _updates.Clear();
        }
    }

    public void Dispose()
    {
        if (_disposeTokenSource.IsCancellationRequested)
        {
            return;
        }

        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();

        foreach (var (_, updateItem) in _updates)
        {
            updateItem.Dispose();
        }

        // Release before dispose to ensure we don't throw exceptions from the background thread trying to release
        // while we're disposing. Multiple releases are fine, and if we release and it lets something passed the lock
        // our cancellation token check will mean its a no-op.
        _semaphore.Release();
        _semaphore.Dispose();

        _blockBackgroundWorkStart?.Set();
    }

    private async Task UpdateWorkspaceStateAsync(Project? workspaceProject, IProjectSnapshot projectSnapshot, CancellationToken cancellationToken)
    {
        if (_disposeTokenSource.IsCancellationRequested)
        {
            return;
        }

        try
        {
            // Only allow a single TagHelper resolver request to process at a time in order to reduce
            // Visual Studio memory pressure. Typically a TagHelper resolution result can be upwards of 10mb+.
            // So if we now do multiple requests to resolve TagHelpers simultaneously it results in only a
            // single one executing at a time so that we don't have N number of requests in flight with these
            // 10mb payloads waiting to be processed.
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Object disposed or task cancelled exceptions should be swallowed/no-op'd
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

            if (workspaceState is null || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await _projectManager
                .UpdateAsync(
                    static (updater, state) =>
                    {
                        var (projectKey, workspaceState, cancellationToken) = state;

                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        updater.ProjectWorkspaceStateChanged(projectKey, workspaceState);
                    },
                    state: (projectSnapshot.Key, workspaceState, cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Abort work if we get a task canceled exception
            return;
        }
        catch (Exception ex)
        {
            _errorReporter.ReportError(ex);
        }
        finally
        {
            try
            {
                // Prevent ObjectDisposedException if we've disposed before we got here. The dispose method will release
                // anyway, so we're all good.
                if (!_disposeTokenSource.IsCancellationRequested)
                {
                    _semaphore.Release();
                }
            }
            catch
            {
                // Swallow exceptions that happen from releasing the semaphore.
            }
        }

        OnBackgroundWorkCompleted();
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

            _errorReporter.ReportError(ex, projectSnapshot);
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
