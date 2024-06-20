// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO.Pipes;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.RoslynWorkspace;

public class RazorWorkspaceListener : IDisposable
{
    private static readonly TimeSpan s_debounceTime = TimeSpan.FromMilliseconds(500);

    private readonly ILogger _logger;
    private Workspace? _workspace;

    // Use an immutable dictionary for ImmutableInterlocked operations. The value isn't checked, just
    // the existance of the key so work is only done for projects with dynamic files.
    private ImmutableDictionary<ProjectId, bool> _projectsWithDynamicFile = ImmutableDictionary<ProjectId, bool>.Empty;
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private readonly AsyncBatchingWorkQueue<Work> _workQueue;

    record Work(ProjectId ProjectId);
    record UpdateWork(ProjectId ProjectId) : Work(ProjectId);
    record RemovalWork(ProjectId ProjectId, string IntermediateOutputPath) : Work(ProjectId);

    private NamedPipeServerStream? _namedPipe;

    public RazorWorkspaceListener(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(RazorWorkspaceListener));
        _workQueue = new(TimeSpan.FromMilliseconds(500), UpdateCurrentProjectsAsync, EqualityComparer<Work>.Default, _disposeTokenSource.Token);
    }

    public void Dispose()
    {
        if (_workspace is not null)
        {
            _workspace.WorkspaceChanged -= Workspace_WorkspaceChanged;
        }

        if (_disposeTokenSource.IsCancellationRequested)
        {
            return;
        }

        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }

    public void EnsureInitialized(Workspace workspace, string pipeName)
    {
        // Make sure we don't hook up the event handler multiple times
        if (_namedPipe is not null)
        {
            return;
        }

        _workspace = workspace;
        _workspace.WorkspaceChanged += Workspace_WorkspaceChanged;

        _logger.LogTrace("Opening named pipe server: {0}", pipeName);
        _namedPipe = new NamedPipeServerStream(pipeName, PipeDirection.Out, maxNumberOfServerInstances: 1, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);
    }

    public void NotifyDynamicFile(ProjectId projectId)
    {
        // Since there is no "un-notify" API to indicate that callers no longer care about a project, it's entirely
        // possible that by the time we get notified, a project might have been removed from the workspace. Whilst
        // that wouldn't cause any issues we may as well avoid creating a task scheduler.
        if (_workspace is null || !_workspace.CurrentSolution.ContainsProject(projectId))
        {
            return;
        }

        ImmutableInterlocked.GetOrAdd(ref _projectsWithDynamicFile, projectId, static (_) => true);

        // Schedule a task, in case adding a dynamic file is the last thing that happens
        _logger.LogTrace("{projectId} scheduling task due to dynamic file", projectId);
        _workQueue.AddWork(new UpdateWork(projectId));
    }

    private void Workspace_WorkspaceChanged(object? sender, WorkspaceChangeEventArgs e)
    {
        switch (e.Kind)
        {
            case WorkspaceChangeKind.SolutionChanged:
            case WorkspaceChangeKind.SolutionReloaded:
                foreach (var project in e.NewSolution.Projects)
                {
                    EnqueueUpdate(project);
                }

                break;

            case WorkspaceChangeKind.SolutionAdded:
                foreach (var project in e.NewSolution.Projects)
                {
                    EnqueueUpdate(project);
                }

                break;

            case WorkspaceChangeKind.ProjectReloaded:
                EnqueueUpdate(e.NewSolution.GetProject(e.ProjectId));
                break;

            case WorkspaceChangeKind.ProjectRemoved:
                RemoveProject(e.OldSolution.GetProject(e.ProjectId.AssumeNotNull()).AssumeNotNull());
                break;

            case WorkspaceChangeKind.ProjectAdded:
            case WorkspaceChangeKind.ProjectChanged:
            case WorkspaceChangeKind.DocumentAdded:
            case WorkspaceChangeKind.DocumentRemoved:
            case WorkspaceChangeKind.DocumentReloaded:
            case WorkspaceChangeKind.DocumentChanged:
            case WorkspaceChangeKind.AdditionalDocumentAdded:
            case WorkspaceChangeKind.AdditionalDocumentRemoved:
            case WorkspaceChangeKind.AdditionalDocumentReloaded:
            case WorkspaceChangeKind.AdditionalDocumentChanged:
            case WorkspaceChangeKind.DocumentInfoChanged:
            case WorkspaceChangeKind.AnalyzerConfigDocumentAdded:
            case WorkspaceChangeKind.AnalyzerConfigDocumentRemoved:
            case WorkspaceChangeKind.AnalyzerConfigDocumentReloaded:
            case WorkspaceChangeKind.AnalyzerConfigDocumentChanged:
                var projectId = e.ProjectId ?? e.DocumentId?.ProjectId;
                if (projectId is not null)
                {
                    EnqueueUpdate(e.NewSolution.GetProject(projectId));
                }

                break;

            case WorkspaceChangeKind.SolutionCleared:
            case WorkspaceChangeKind.SolutionRemoved:
                foreach (var project in e.OldSolution.Projects)
                {
                    RemoveProject(project);
                }

                break;

            default:
                break;
        }
    }

    private void RemoveProject(Project project)
    {
        if (ImmutableInterlocked.TryRemove(ref _projectsWithDynamicFile, project.Id, out var _))
        {
            var intermediateOutputPath = Path.GetDirectoryName(project.CompilationOutputInfo.AssemblyPath);
            if (intermediateOutputPath is null)
            {
                _logger?.LogTrace("intermediatePath is null, skipping notification of removal for {projectId}", project.Id);
                return;
            }

            _workQueue.AddWork(new RemovalWork(project.Id, intermediateOutputPath));   
        }
    }

    private void EnqueueUpdate(Project? project)
    {
        if (_namedPipe is null ||
            project is not
            {
                Language: LanguageNames.CSharp
            })
        {
            return;
        }

        // Don't queue work for projects that don't have a dynamic file
        if (!_projectsWithDynamicFile.TryGetValue(project.Id, out var _))
        {
            return;
        }

        var projectId = project.Id;
        _workQueue.AddWork(new UpdateWork(projectId));
    }

    private async ValueTask UpdateCurrentProjectsAsync(ImmutableArray<Work> work, CancellationToken cancellationToken)
    {
        var solution = _workspace.AssumeNotNull().CurrentSolution;
        _namedPipe.AssumeNotNull();

        if (!_namedPipe.IsConnected)
        {
            await _namedPipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var unit in work)
        {
            try
            {
                if (_disposeTokenSource.IsCancellationRequested)
                {
                    return;
                }

                if (unit is RemovalWork removalWork)
                {
                    await ReportRemovalAsync(removalWork, cancellationToken).ConfigureAwait(false);
                }

                var project = solution.GetProject(unit.ProjectId);
                if (project is null)
                {
                    _logger?.LogTrace("Project {projectId} is not in workspace", unit.ProjectId);
                    continue;
                }

                await UpdateProjectAsync(project, solution, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Encountered exception while processing unit: {message}", ex.Message);
            }
        }

        await _namedPipe.AssumeNotNull().FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task ReportRemovalAsync(RemovalWork unit, CancellationToken cancellationToken)
    {
        _logger?.LogTrace("Reporting removal of {projectId}", unit.ProjectId);

        _namedPipe.AssumeNotNull().WriteByte(0);
        return _namedPipe.WriteStringAsync(unit.IntermediateOutputPath, cancellationToken: cancellationToken);
    }

    // private protected for testing
    private protected virtual async Task UpdateProjectAsync(Project project, Solution solution, CancellationToken cancellationToken)
    {
        _logger?.LogTrace("Serializing information for {projectId}", project.Id);
        var projectInfo = await RazorProjectInfoFactory.ConvertAsync(project, _logger, cancellationToken).ConfigureAwait(false);
        if (projectInfo is null)
        {
            return;
        }

        var bytes = projectInfo.Serialize();
        var sizeBytes = BitConverter.GetBytes(bytes.Length);
        _namedPipe.AssumeNotNull();

        _logger?.LogTrace("Writing 1 to indicate update");
        _namedPipe.WriteByte(1);

        _logger?.LogTrace("Update size is {bytes} bytes and {tagHelpers} TagHelpers", bytes.Length, projectInfo.ProjectWorkspaceState.TagHelpers.Length);
        await _namedPipe.WriteAsync(sizeBytes, 0, sizeBytes.Length, cancellationToken).ConfigureAwait(false);
        await _namedPipe.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
    }
}
