// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Utilities;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

/// <summary>
/// Updates project system service with new project info.
/// </summary>
/// <remarks>
/// Used to figure out if the project system data is being added, updated or deleted
/// and acts accordingly to update project system service.
/// </remarks>
internal partial class ProjectConfigurationStateManager : IDisposable
{
    private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
    private readonly IRazorProjectService _projectService;
    private readonly IProjectSnapshotManager _projectManager;
    private readonly ILogger _logger;

    private readonly AsyncBatchingWorkQueue<(ProjectKey ProjectKey, RazorProjectInfo? ProjectInfo)> _workQueue;
    private readonly CancellationTokenSource _disposalTokenSource;

    public ProjectConfigurationStateManager(
        ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
        IRazorProjectService projectService,
        ILoggerFactory loggerFactory,
        IProjectSnapshotManager projectManager)
    {
        _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
        _projectService = projectService;
        _projectManager = projectManager;
        _logger = loggerFactory.GetOrCreateLogger<ProjectConfigurationStateManager>();

        _disposalTokenSource = new ();
        _workQueue = new(
            EnqueueDelay,
            ProcessBatchAsync,
            _disposalTokenSource.Token);
    }

    private ValueTask ProcessBatchAsync(ImmutableArray<(ProjectKey ProjectKey, RazorProjectInfo? ProjectInfo)> workItems, CancellationToken cancellationToken)
    {
        foreach (var workItem in workItems.GetMostRecentUniqueItems(Comparer.Instance))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return default;
            }

            UpdateProject(workItem.ProjectKey, workItem.ProjectInfo);
        }

        return default;
    }

    internal TimeSpan EnqueueDelay { get; set; } = TimeSpan.FromMilliseconds(250);

    public Task ProjectInfoUpdatedAsync(ProjectKey projectKey, RazorProjectInfo? projectInfo, CancellationToken cancellationToken)
    {
        return _projectSnapshotManagerDispatcher.RunAsync(
            () => ProjectInfoUpdatedImpl(projectKey, projectInfo),
            cancellationToken);
    }

    private void ProjectInfoUpdatedImpl(ProjectKey projectKey, RazorProjectInfo? projectInfo)
    {
        _projectSnapshotManagerDispatcher.AssertRunningOnDispatcher();

        var knownProject = _projectManager.TryGetLoadedProject(projectKey, out _);

        if (projectInfo is null)
        {
            if (knownProject)
            {
                // projectInfo null is "remove"
                EnqueueUpdateProject(projectKey, projectInfo: null);
            }
            else
            {
                _logger.LogWarning("Found no existing project key '{0}' but projectInfo is null. Assuming no-op deletion.", projectKey.Id);
            }

            return;
        }

        if (!knownProject)
        {
            // For the new code path using endpoint, SerializedFilePath is IntermediateOutputPath.
            // We will rename it when we remove the old code path that actually uses serialized bin file.
            var intermediateOutputPath = FilePathNormalizer.Normalize(projectInfo.SerializedFilePath);
            _logger.LogInformation("Found no existing project key for project key '{0}'. Assuming new project.", projectKey.Id);

            AddProject(intermediateOutputPath, projectInfo);
            return;
        }

        _logger.LogInformation("Project info changed for project '{0}'", projectKey.Id);

        EnqueueUpdateProject(projectKey, projectInfo);
    }

    private void AddProject(string intermediateOutputPath, RazorProjectInfo projectInfo)
    {
        var projectFilePath = FilePathNormalizer.Normalize(projectInfo.FilePath);
        var rootNamespace = projectInfo.RootNamespace;

        var projectKey = _projectService.AddProject(projectFilePath, intermediateOutputPath, projectInfo.Configuration, rootNamespace, projectInfo.DisplayName);

        _logger.LogInformation("Project configuration file added for project '{0}': '{1}'", projectFilePath, intermediateOutputPath);
        EnqueueUpdateProject(projectKey, projectInfo);
    }

    private void UpdateProject(ProjectKey projectKey, RazorProjectInfo? projectInfo)
    {
        if (projectInfo is null)
        {
            ResetProject(projectKey);
            return;
        }

        _logger.LogInformation("Actually updating {project} with a real projectInfo", projectKey);

        var projectWorkspaceState = projectInfo.ProjectWorkspaceState ?? ProjectWorkspaceState.Default;
        var documents = projectInfo.Documents;
        _projectService.UpdateProject(
            projectKey,
            projectInfo.Configuration,
            projectInfo.RootNamespace,
            projectInfo.DisplayName,
            projectWorkspaceState,
            documents);
    }

    private void EnqueueUpdateProject(ProjectKey projectKey, RazorProjectInfo? projectInfo)
    {
        _workQueue.AddWork((projectKey, projectInfo));
    }

    private void ResetProject(ProjectKey projectKey)
    {
        _projectService.UpdateProject(
            projectKey,
            configuration: null,
            rootNamespace: null,
            displayName: "",
            ProjectWorkspaceState.Default,
            ImmutableArray<DocumentSnapshotHandle>.Empty);
    }

    public void Dispose()
    {
        _disposalTokenSource.Cancel();
        _disposalTokenSource.Dispose();
    }
}
