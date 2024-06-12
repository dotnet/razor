// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.RoslynWorkspace;

public class RazorWorkspaceListener : IDisposable
{
    private static readonly TimeSpan s_debounceTime = TimeSpan.FromMilliseconds(500);

    private readonly ILogger _logger;

    private string? _projectInfoFileName;
    private Workspace? _workspace;
    private ImmutableArray<ProjectId> _currentProjects = ImmutableArray<ProjectId>.Empty;
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private readonly AsyncBatchingWorkQueue<ProjectId> _workQueue;

    public RazorWorkspaceListener(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(nameof(RazorWorkspaceListener));
        _workQueue = new(TimeSpan.FromMilliseconds(500), UpdateCurrentProjectsAsync, EqualityComparer<ProjectId>.Default, _disposeTokenSource.Token);
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

    public void EnsureInitialized(Workspace workspace, string projectInfoFileName)
    {
        // Make sure we don't hook up the event handler multiple times
        if (_projectInfoFileName is not null)
        {
            return;
        }

        _projectInfoFileName = projectInfoFileName;
        _workspace = workspace;
        _workspace.WorkspaceChanged += Workspace_WorkspaceChanged;
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

        // Schedule a task, in case adding a dynamic file is the last thing that happens
        _logger.LogTrace("{projectId} scheduling task due to dynamic file", projectId);
        _workQueue.AddWork(projectId);
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
            default:
                break;
        }
    }

    private void EnqueueUpdate(Project? project)
    {
        if (_projectInfoFileName is null ||
            project is not
            {
                Language: LanguageNames.CSharp
            })
        {
            return;
        }

        var projectId = project.Id;
        _workQueue.AddWork(projectId);
    }

    private async ValueTask UpdateCurrentProjectsAsync(ImmutableArray<ProjectId> projectIds, CancellationToken cancellationToken)
    {
        var solution = _workspace.AssumeNotNull().CurrentSolution;

        foreach (var projectId in projectIds)
        {
            if (_disposeTokenSource.IsCancellationRequested)
            {
                return;
            }

            await SerializeProjectAsync(projectId, solution, cancellationToken).ConfigureAwait(false);
        }
    }

    // Protected for testing
    internal protected virtual Task SerializeProjectAsync(ProjectId projectId, Solution solution, CancellationToken cancellationToken)
    {
        var project = solution.GetProject(projectId);
        if (project is null)
        {
            _logger?.LogTrace("Project {projectId} is not in workspace", projectId);
            return Task.CompletedTask;
        }

        _logger?.LogTrace("Serializing information for {projectId}", projectId);
        return RazorProjectInfoSerializer.SerializeAsync(project, _projectInfoFileName.AssumeNotNull(), _logger, cancellationToken);
    }
}
