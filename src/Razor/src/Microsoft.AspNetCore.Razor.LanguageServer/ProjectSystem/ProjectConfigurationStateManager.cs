// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

/// <summary>
/// Updates project system service with new project info.
/// </summary>
/// <remarks>
/// Used to figure out if the project system data is being added, updated or deleted
/// and acts accordingly to update project system service.
/// </remarks>
internal class ProjectConfigurationStateManager
{
    private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
    private readonly IRazorProjectService _projectService;
    private readonly IProjectSnapshotManager _projectManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Used to throttle project system updates
    /// </summary>
    internal readonly Dictionary<ProjectKey, DelayedProjectInfo> ProjectInfoMap;

    public ProjectConfigurationStateManager(
        ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
        IRazorProjectService projectService,
        IProjectSnapshotManager projectManager,
        IRazorLoggerFactory loggerFactory)
    {
        _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
        _projectService = projectService;
        _projectManager = projectManager;
        _logger = loggerFactory.CreateLogger<ProjectConfigurationStateManager>();

        ProjectInfoMap = new Dictionary<ProjectKey, DelayedProjectInfo>();
    }

    internal int EnqueueDelay { get; set; } = 250;

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
            var configurationFilePath = FilePathNormalizer.Normalize(projectInfo.SerializedFilePath);
            _logger.LogInformation("Found no existing project key for project key '{0}'. Assuming new project.", projectKey.Id);

            AddProject(configurationFilePath, projectInfo);
            return;
        }

        _logger.LogInformation("Project info changed for project '{0}'", projectKey.Id);

        EnqueueUpdateProject(projectKey, projectInfo);
    }

    private void AddProject(string configurationFilePath, RazorProjectInfo projectInfo)
    {
        var projectFilePath = FilePathNormalizer.Normalize(projectInfo.FilePath);
        var intermediateOutputPath = Path.GetDirectoryName(configurationFilePath).AssumeNotNull();
        var rootNamespace = projectInfo.RootNamespace;

        var projectKey = _projectService.AddProject(projectFilePath, intermediateOutputPath, projectInfo.Configuration, rootNamespace, projectInfo.DisplayName);

        _logger.LogInformation("Project configuration file added for project '{0}': '{1}'", projectFilePath, configurationFilePath);
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

    private async Task UpdateAfterDelayAsync(ProjectKey projectKey)
    {
        await Task.Delay(EnqueueDelay).ConfigureAwait(true);

        var delayedProjectInfo = ProjectInfoMap[projectKey];
        UpdateProject(projectKey, delayedProjectInfo.ProjectInfo);
    }

    private void EnqueueUpdateProject(ProjectKey projectKey, RazorProjectInfo? projectInfo)
    {
        if (!ProjectInfoMap.ContainsKey(projectKey))
        {
            ProjectInfoMap[projectKey] = new DelayedProjectInfo();
        }

        var delayedProjectInfo = ProjectInfoMap[projectKey];
        delayedProjectInfo.ProjectInfo = projectInfo;

        if (delayedProjectInfo.ProjectUpdateTask is null || delayedProjectInfo.ProjectUpdateTask.IsCompleted)
        {
            delayedProjectInfo.ProjectUpdateTask = UpdateAfterDelayAsync(projectKey);
        }
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

    internal class DelayedProjectInfo
    {
        public Task? ProjectUpdateTask { get; set; }

        public RazorProjectInfo? ProjectInfo { get; set; }
    }
}
