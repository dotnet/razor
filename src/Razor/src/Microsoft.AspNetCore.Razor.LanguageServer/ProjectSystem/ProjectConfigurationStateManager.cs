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
using Microsoft.VisualStudio.Threading;

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
    private readonly IRazorProjectService _projectService;
    private readonly IProjectSnapshotManager _projectManager;
    private readonly ILogger _logger;

    private readonly AsyncBatchingWorkQueue<(ProjectKey ProjectKey, RazorProjectInfo? ProjectInfo)> _workQueue;
    private readonly CancellationTokenSource _disposalTokenSource;
    private static readonly TimeSpan s_enqueueDelay = TimeSpan.FromMilliseconds(250);

    public ProjectConfigurationStateManager(
        IRazorProjectService projectService,
        ILoggerFactory loggerFactory,
        IProjectSnapshotManager projectManager)
        : this(projectService,
               loggerFactory,
               projectManager,
               s_enqueueDelay)
    { }

    // Provided for tests to specify enqueue delay
    public ProjectConfigurationStateManager(
        IRazorProjectService projectService,
        ILoggerFactory loggerFactory,
        IProjectSnapshotManager projectManager,
        TimeSpan enqueueDelay)
    {
        _projectService = projectService;
        _projectManager = projectManager;
        _logger = loggerFactory.GetOrCreateLogger<ProjectConfigurationStateManager>();

        _disposalTokenSource = new ();
        _workQueue = new(
            enqueueDelay,
            ProcessBatchAsync,
            _disposalTokenSource.Token);
    }

    public void Dispose()
    {
        _disposalTokenSource.Cancel();
        _disposalTokenSource.Dispose();
    }

    private ValueTask ProcessBatchAsync(ImmutableArray<(ProjectKey ProjectKey, RazorProjectInfo? ProjectInfo)> workItems, CancellationToken cancellationToken)
    {
        foreach (var workItem in workItems.GetMostRecentUniqueItems(Comparer.Instance))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return default;
            }

            UpdateProject(workItem.ProjectKey, workItem.ProjectInfo, cancellationToken);
        }

        return default;
    }

    public void ProjectInfoUpdated(ProjectKey projectKey, RazorProjectInfo? projectInfo, CancellationToken cancellationToken)
    {
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
                _logger.LogWarning($"Found no existing project key '{projectKey.Id}' but projectInfo is null. Assuming no-op deletion.");
            }

            return;
        }

        if (!knownProject)
        {
            // For the new code path using endpoint, SerializedFilePath is IntermediateOutputPath.
            // We will rename it when we remove the old code path that actually uses serialized bin file.
            var intermediateOutputPath = FilePathNormalizer.Normalize(projectInfo.SerializedFilePath);
            _logger.LogInformation($"Found no existing project key for project key '{projectKey.Id}'. Assuming new project.");

            AddProjectAsync(
                intermediateOutputPath,
                projectInfo,
                cancellationToken).Forget();
            return;
        }

        _logger.LogInformation($"Project info changed for project '{projectKey.Id}'");

        EnqueueUpdateProject(projectKey, projectInfo);
    }

    private async Task AddProjectAsync(
        string intermediateOutputPath,
        RazorProjectInfo projectInfo,
        CancellationToken cancellationToken)
    {
        var projectFilePath = FilePathNormalizer.Normalize(projectInfo.FilePath);
        var rootNamespace = projectInfo.RootNamespace;

        var projectKey = await _projectService.AddProjectAsync(
            projectFilePath,
            intermediateOutputPath,
            projectInfo.Configuration,
            rootNamespace,
            projectInfo.DisplayName,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation($"Project configuration file added for project '{projectFilePath}': '{intermediateOutputPath}'");
        EnqueueUpdateProject(projectKey, projectInfo);
    }

    private void UpdateProject(ProjectKey projectKey, RazorProjectInfo? projectInfo, CancellationToken cancellationToken)
    {
        if (projectInfo is null)
        {
            ResetProject(projectKey, cancellationToken);
            return;
        }

        _logger.LogInformation($"Actually updating {projectKey} with a real projectInfo");

        var projectWorkspaceState = projectInfo.ProjectWorkspaceState ?? ProjectWorkspaceState.Default;
        var documents = projectInfo.Documents;
        _projectService.UpdateProjectAsync(
            projectKey,
            projectInfo.Configuration,
            projectInfo.RootNamespace,
            projectInfo.DisplayName,
            projectWorkspaceState,
            documents,
            cancellationToken).Forget();
    }

    private void EnqueueUpdateProject(ProjectKey projectKey, RazorProjectInfo? projectInfo)
    {
        _workQueue.AddWork((projectKey, projectInfo));
    }

    private void ResetProject(ProjectKey projectKey, CancellationToken cancellationToken)
    {
        _projectService.UpdateProjectAsync(
            projectKey,
            configuration: null,
            rootNamespace: null,
            displayName: "",
            ProjectWorkspaceState.Default,
            ImmutableArray<DocumentSnapshotHandle>.Empty,
            cancellationToken).Forget();
    }
}
