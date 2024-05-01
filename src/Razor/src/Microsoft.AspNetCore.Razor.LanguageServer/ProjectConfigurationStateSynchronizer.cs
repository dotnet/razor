// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal partial class ProjectConfigurationStateSynchronizer : IProjectConfigurationFileChangeListener, IDisposable
{
    private static readonly TimeSpan s_delay = TimeSpan.FromMilliseconds(250);

    private readonly IRazorProjectService _projectService;
    private readonly LanguageServerFeatureOptions _options;
    private readonly ILogger _logger;

    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly AsyncBatchingWorkQueue<Work> _workQueue;

    private readonly Dictionary<ProjectKey, ResetProject> _resetProjectMap = new();

    public ProjectConfigurationStateSynchronizer(
        IRazorProjectService projectService,
        ILoggerFactory loggerFactory,
        LanguageServerFeatureOptions options)
        : this(projectService, loggerFactory, options, s_delay)
    {
    }

    protected ProjectConfigurationStateSynchronizer(
        IRazorProjectService projectService,
        ILoggerFactory loggerFactory,
        LanguageServerFeatureOptions options,
        TimeSpan delay)
    {
        _projectService = projectService;
        _options = options;
        _logger = loggerFactory.GetOrCreateLogger<ProjectConfigurationStateSynchronizer>();

        _disposeTokenSource = new();
        _workQueue = new(delay, ProcessBatchAsync, _disposeTokenSource.Token);
    }

    public void Dispose()
    {
        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }
    private async ValueTask ProcessBatchAsync(ImmutableArray<Work> items, CancellationToken token)
    {
        // Clear out our helper collections.
        _resetProjectMap.Clear();

        var combinedItems = ConvertRemoveThenAddToUpdate(items, token);

        if (token.IsCancellationRequested)
        {
            return;
        }

        foreach (var item in combinedItems.GetMostRecentUniqueItems(Comparer.Instance))
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (item.Skip)
            {
                continue;
            }

            var itemTask = item switch
            {
                AddProject(var projectInfo) => AddProjectAsync(projectInfo, token),
                ResetProject(var projectKey) => ResetProjectAsync(projectKey, token),
                UpdateProject(var projectKey, var projectInfo) => UpdateProjectAsync(projectKey, projectInfo, token),
                _ => Assumed.Unreachable<Task>()
            };

            await itemTask.ConfigureAwait(false);
        }

        async Task AddProjectAsync(RazorProjectInfo projectInfo, CancellationToken token)
        {
            var projectFilePath = FilePathNormalizer.Normalize(projectInfo.FilePath);
            var intermediateOutputPath = FilePathNormalizer.GetNormalizedDirectoryName(projectInfo.SerializedFilePath);

            // If the project already exists, this will be a no-op
            var projectKey = await _projectService
                .AddProjectAsync(
                    projectFilePath,
                    intermediateOutputPath,
                    projectInfo.Configuration,
                    projectInfo.RootNamespace,
                    projectInfo.DisplayName,
                    token)
                .ConfigureAwait(false);

            _logger.LogInformation($"Project configuration file added for project key {projectKey.Id}, file '{projectFilePath}': '{projectInfo.SerializedFilePath}'");

            await UpdateProjectAsync(projectKey, projectInfo, token).ConfigureAwait(false);
        }

        Task ResetProjectAsync(ProjectKey projectKey, CancellationToken token)
        {
            _logger.LogInformation($"Resetting {projectKey.Id}.");

            return _projectService
                .UpdateProjectAsync(
                    projectKey,
                    configuration: null,
                    rootNamespace: null,
                    displayName: "",
                    ProjectWorkspaceState.Default,
                    documents: [],
                    token);
        }

        Task UpdateProjectAsync(ProjectKey projectKey, RazorProjectInfo projectInfo, CancellationToken token)
        {
            _logger.LogInformation($"Updating {projectKey.Id}.");

            return _projectService
                .UpdateProjectAsync(
                    projectKey,
                    projectInfo.Configuration,
                    projectInfo.RootNamespace,
                    projectInfo.DisplayName,
                    projectInfo.ProjectWorkspaceState,
                    projectInfo.Documents,
                    token);
        }

        ImmutableArray<Work> ConvertRemoveThenAddToUpdate(ImmutableArray<Work> items, CancellationToken token)
        {
            using var itemsToProcess = new PooledArrayBuilder<Work>(items.Length);

            var skippedAny = false;
            foreach (var item in items)
            {
                if (token.IsCancellationRequested)
                {
                    return ImmutableArray<Work>.Empty;
                }

                switch (item)
                {
                    case ResetProject(var projectKey) reset:
                        // If there was a previous Reset for this project, we want to remove it from the map
                        // so it can't be skipped, because it must be genuine for there to be another reset
                        // after it (though realistically this would be an odd set of file events to get)
                        _resetProjectMap[projectKey] = reset;
                        itemsToProcess.Add(reset);

                        break;

                    case AddProject(var projectKey, var projectInfo)
                    when _resetProjectMap.TryGetValue(projectKey, out var previousReset):
                        // We've already seen a Reset for this file path and now we have an Add, so let's convert to an Update
                        // and skip the Reset.
                        previousReset.Skip = true;

                        skippedAny = true;

                        var update = new UpdateProject(projectKey, projectInfo);
                        itemsToProcess.Add(update);

                        // Remove from the project map in case we see another Remove-Add set later
                        _resetProjectMap.Remove(projectKey);

                        break;

                    default:
                        itemsToProcess.Add(item);

                        break;
                }
            }

            // If there is nothing to skip, we did nothing, so return the original list
            if (!skippedAny)
            {
                return items;
            }

            return itemsToProcess.DrainToImmutable();
        }
    }

    public void ProjectConfigurationFileChanged(ProjectConfigurationFileChangeEventArgs args)
    {
        switch (args.Kind)
        {
            case RazorFileChangeKind.Changed:
                {
                    if (args.TryDeserialize(_options, out var projectInfo))
                    {
                        var projectKey = ProjectKey.From(projectInfo);
                        _logger.LogInformation($"Configuration file changed for project '{projectKey.Id}'.");

                        // UpdateProject will no-op if the project isn't known
                        _workQueue.AddWork(new UpdateProject(projectKey, projectInfo));
                    }
                    else
                    {
                        var projectKey = args.GetProjectKey();
                        _logger.LogWarning($"Failed to deserialize after change to configuration file for project '{projectKey.Id}'.");

                        // We found the last associated project file for the configuration file. Reset the project since we can't
                        // accurately determine its configurations.
                        _workQueue.AddWork(new ResetProject(projectKey));
                    }
                }

                break;

            case RazorFileChangeKind.Added:
                {
                    if (args.TryDeserialize(_options, out var projectInfo))
                    {
                        _workQueue.AddWork(new AddProject(projectInfo));
                    }
                    else
                    {
                        // This is the first time we've seen this configuration file, but we can't deserialize it.
                        // The only thing we can really do is issue a warning.
                        _logger.LogWarning($"Failed to deserialize previously unseen configuration file '{args.ConfigurationFilePath}'");
                    }
                }

                break;

            case RazorFileChangeKind.Removed:
                {
                    var projectKey = args.GetProjectKey();
                    _logger.LogInformation($"Configuration file removed for project '{projectKey}'.");

                    _workQueue.AddWork(new ResetProject(projectKey));
                }

                break;
        }
    }
}
