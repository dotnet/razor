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
using Microsoft.VisualStudio.Threading;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal partial class ProjectConfigurationStateSynchronizer : IProjectConfigurationFileChangeListener, IDisposable
{
    private abstract record Work(ProjectKey ProjectKey);
    private sealed record AddProject(RazorProjectInfo ProjectInfo) : Work(ProjectKey.From(ProjectInfo));
    private sealed record ResetProject(ProjectKey ProjectKey) : Work(ProjectKey);
    private sealed record UpdateProject(ProjectKey ProjectKey, RazorProjectInfo ProjectInfo) : Work(ProjectKey);

    private static readonly TimeSpan s_delay = TimeSpan.FromMilliseconds(250);

    private readonly IRazorProjectService _projectService;
    private readonly LanguageServerFeatureOptions _options;
    private readonly ILogger _logger;

    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly AsyncBatchingWorkQueue<Work> _workQueue;

    private readonly Dictionary<ProjectKey, (ResetProject work, int index)> _resetProjectMap = new();
    private readonly List<int> _indicesToSkip = new();

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
        _indicesToSkip.Clear();

        using var itemsToProcess = new PooledArrayBuilder<Work>(items.Length);
        var index = 0;

        foreach (var item in items)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (item is ResetProject reset)
            {
                // We shouldn't ever get two resets for the same project, due to GetMostRecentUniqueItems, so the dictionary Add
                // should always be safe
                Debug.Assert(!_resetProjectMap.ContainsKey(reset.ProjectKey));

                _resetProjectMap.Add(reset.ProjectKey, (reset, index));
                itemsToProcess.Add(reset);
            }
            else if (item is AddProject add &&
                _resetProjectMap.TryGetValue(add.ProjectKey, out var previousReset))
            {
                // We've already seen a Reset for this file path and now we have an Add, so let's convert to an Update
                // and skip the Reset.
                _indicesToSkip.Add(previousReset.index);

                var update = new UpdateProject(previousReset.work.ProjectKey, add.ProjectInfo);
                itemsToProcess.Add(update);
                _resetProjectMap.Remove(add.ProjectKey);
            }
            else
            {
                itemsToProcess.Add(item);
            }

            index++;
        }

        // Now that we've got the real items we want to process, we remove the items we want to skip
        // and then we can process the most recent unique items as normal
        for (var i = _indicesToSkip.Count - 1; i >= 0; i--)
        {
            itemsToProcess.RemoveAt(_indicesToSkip[i]);
        }

        var finalItems = itemsToProcess.DrainToImmutable();

        foreach (var item in finalItems.GetMostRecentUniqueItems(Comparer.Instance))
        {
            if (token.IsCancellationRequested)
            {
                return;
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
                        var projectKey = ProjectKey.FromString(FilePathNormalizer.GetNormalizedDirectoryName(args.ConfigurationFilePath));
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
                    var projectKey = ProjectKey.FromString(FilePathNormalizer.GetNormalizedDirectoryName(args.ConfigurationFilePath));
                    _logger.LogInformation($"Configuration file removed for project '{projectKey}'.");

                    _workQueue.AddWork(new ResetProject(projectKey));
                }

                break;
        }
    }
}
