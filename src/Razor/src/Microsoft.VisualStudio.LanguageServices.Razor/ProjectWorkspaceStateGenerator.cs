// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
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
    // Internal for testing
    internal readonly Dictionary<ProjectKey, UpdateItem> Updates = new();

    private readonly IProjectSnapshotManager _projectManager = projectManager;
    private readonly ITagHelperResolver _tagHelperResolver = tagHelperResolver;
    private readonly IErrorReporter _errorReporter = errorReporter;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;
    private readonly SemaphoreSlim _semaphore = new(initialCount: 1);

    private readonly CancellationTokenSource _disposeTokenSource = new();

    // Used in unit tests to ensure we can control when background work starts.
    public ManualResetEventSlim? BlockBackgroundWorkStart { get; set; }

    // Used in unit tests to ensure we can know when background work finishes.
    public ManualResetEventSlim? NotifyBackgroundWorkCompleted { get; set; }

    public void EnqueueUpdate(Project? workspaceProject, IProjectSnapshot projectSnapshot)
    {
        if (_disposeTokenSource.IsCancellationRequested)
        {
            return;
        }

        lock (Updates)
        {
            if (Updates.TryGetValue(projectSnapshot.Key, out var updateItem))
            {
                updateItem.Dispose();
            }

            Updates[projectSnapshot.Key] = new UpdateItem(
                token => UpdateWorkspaceStateAsync(workspaceProject, projectSnapshot, token),
                _disposeTokenSource.Token);
        }
    }

    public void CancelUpdates()
    {
        lock (Updates)
        {
            foreach (var (_, updateItem) in Updates)
            {
                updateItem.Dispose();
            }

            Updates.Clear();
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

        foreach (var (_, updateItem) in Updates)
        {
            updateItem.Dispose();
        }

        // Release before dispose to ensure we don't throw exceptions from the background thread trying to release
        // while we're disposing. Multiple releases are fine, and if we release and it lets something passed the lock
        // our cancellation token check will mean its a no-op.
        _semaphore.Release();
        _semaphore.Dispose();

        BlockBackgroundWorkStart?.Set();
    }

    private async Task UpdateWorkspaceStateAsync(Project? workspaceProject, IProjectSnapshot projectSnapshot, CancellationToken cancellationToken)
    {
        // We fire this up on a background thread so we could have been disposed already, and if so, waiting on our semaphore
        // throws an exception.
        if (_disposeTokenSource.IsCancellationRequested)
        {
            return;
        }

        // Specifically not using BeginBlock because we want to capture cases where tag helper discovery never finishes.
        var telemetryId = Guid.NewGuid();
        _telemetryReporter.ReportEvent("taghelperresolve/begin", Severity.Normal,
            new Property("id", telemetryId),
            new Property("tagHelperCount", projectSnapshot.ProjectWorkspaceState.TagHelpers.Length));

        try
        {
            // Only allow a single TagHelper resolver request to process at a time in order to reduce Visual Studio memory pressure. Typically a TagHelper resolution result can be upwards of 10mb+.
            // So if we now do multiple requests to resolve TagHelpers simultaneously it results in only a single one executing at a time so that we don't have N number of requests in flight with these
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
                // Silently cancel, we're the only ones creating these tasks.
                return;
            }

            var workspaceState = ProjectWorkspaceState.Default;
            try
            {
                if (workspaceProject != null)
                {
                    var csharpLanguageVersion = LanguageVersion.Default;
                    var csharpParseOptions = workspaceProject.ParseOptions as CSharpParseOptions;
                    if (csharpParseOptions is null)
                    {
                        Debug.Fail("Workspace project should always have CSharp parse options.");
                    }
                    else
                    {
                        csharpLanguageVersion = csharpParseOptions.LanguageVersion;
                    }

                    using var _ = StopwatchPool.GetPooledObject(out var watch);

                    watch.Restart();
                    var tagHelpers = await _tagHelperResolver.GetTagHelpersAsync(workspaceProject, projectSnapshot, cancellationToken).ConfigureAwait(false);
                    watch.Stop();

                    // don't report success if the work was cancelled
                    cancellationToken.ThrowIfCancellationRequested();

                    _telemetryReporter.ReportEvent("taghelperresolve/end", Severity.Normal,
                        new Property("id", telemetryId),
                        new Property("ellapsedms", watch.ElapsedMilliseconds),
                        new Property("result", "success"),
                        new Property("tagHelperCount", tagHelpers.Length));

                    workspaceState = ProjectWorkspaceState.Create(tagHelpers, csharpLanguageVersion);
                }
            }
            catch (OperationCanceledException)
            {
                // Abort work if we get a task cancelled exception
                _telemetryReporter.ReportEvent("taghelperresolve/end", Severity.Normal,
                    new Property("id", telemetryId),
                    new Property("result", "cancel"));
                return;
            }
            catch (Exception ex)
            {
                _telemetryReporter.ReportEvent("taghelperresolve/end", Severity.Normal,
                    new Property("id", telemetryId),
                    new Property("result", "error"));

                _errorReporter.ReportError(ex, projectSnapshot);
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                // Silently cancel, we're the only ones creating these tasks.
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
}
