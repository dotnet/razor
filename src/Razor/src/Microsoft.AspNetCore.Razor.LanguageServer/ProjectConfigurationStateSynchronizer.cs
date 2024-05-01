// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Utilities;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal partial class ProjectConfigurationStateSynchronizer : IProjectConfigurationFileChangeListener, IDisposable
{
    private abstract record Work(ProjectKey ProjectKey);
    private sealed record ResetProject(ProjectKey ProjectKey) : Work(ProjectKey);
    private sealed record UpdateProject(ProjectKey ProjectKey, RazorProjectInfo ProjectInfo) : Work(ProjectKey);

    private static readonly TimeSpan s_delay = TimeSpan.FromMilliseconds(250);

    private readonly IRazorProjectService _projectService;
    private readonly IProjectSnapshotManager _projectManager;
    private readonly LanguageServerFeatureOptions _options;
    private readonly ILogger _logger;

    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly AsyncBatchingWorkQueue<Work> _workQueue;

    public ProjectConfigurationStateSynchronizer(
        IRazorProjectService projectService,
        IProjectSnapshotManager projectManager,
        ILoggerFactory loggerFactory,
        LanguageServerFeatureOptions options)
        : this(projectService, projectManager, loggerFactory, options, s_delay)
    {
    }

    protected ProjectConfigurationStateSynchronizer(
        IRazorProjectService projectService,
        IProjectSnapshotManager projectManager,
        ILoggerFactory loggerFactory,
        LanguageServerFeatureOptions options,
        TimeSpan delay)
    {
        _projectService = projectService;
        _projectManager = projectManager;
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
        foreach (var item in items.GetMostRecentUniqueItems(Comparer.Instance))
        {
            var itemTask = item switch
            {
                ResetProject(var projectKey) => ResetProjectAsync(projectKey, token),
                UpdateProject(var projectKey, var projectInfo) => UpdateProjectAsync(projectKey, projectInfo, token),
                _ => Assumed.Unreachable<Task>()
            };

            await itemTask.ConfigureAwait(false);
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
        // The 
        var intermediateOutputPath = FilePathNormalizer.GetNormalizedDirectoryName(args.ConfigurationFilePath);
        var projectKey = ProjectKey.From(intermediateOutputPath);

        switch (args.Kind)
        {
            case RazorFileChangeKind.Changed:
                {
                    if (args.TryDeserialize(_options, out var projectInfo))
                    {
                        if (_projectManager.TryGetLoadedProject(projectKey, out _))
                        {
                            _logger.LogInformation($"""
                                Configuration file changed for project '{projectKey.Id}'.
                                Configuration file path: '{args.ConfigurationFilePath}'
                                """);

                            _workQueue.AddWork(new UpdateProject(projectKey, projectInfo));
                        }
                        else
                        {
                            _logger.LogWarning($"""
                                Adding project for previously unseen configuration file.
                                Configuration file path: '{args.ConfigurationFilePath}'
                                """);

                            AddProject(projectKey, projectInfo, _disposeTokenSource.Token);
                        }
                    }
                    else
                    {
                        if (_projectManager.TryGetLoadedProject(projectKey, out _))
                        {
                            _logger.LogWarning($"""
                                Failed to deserialize after change to configuration file for project '{projectKey.Id}'.
                                Configuration file path: '{args.ConfigurationFilePath}'
                                """);

                            // We a project for configuration file. Reset is since we couldn't deserialize the info.

                            _workQueue.AddWork(new ResetProject(projectKey));
                        }
                        else
                        {
                            // Could not resolve an associated project file.
                            _logger.LogWarning($"""
                                Failed to deserialize after change to previously unseen configuration file.
                                Configuration file path: '{args.ConfigurationFilePath}'
                                """);
                        }
                    }
                }

                break;

            case RazorFileChangeKind.Added:
                {
                    if (args.TryDeserialize(_options, out var projectInfo))
                    {
                        AddProject(projectKey, projectInfo, _disposeTokenSource.Token);
                    }
                    else
                    {
                        // This is the first time we've seen this configuration file, but we can't deserialize it.
                        // The only thing we can really do is issue a warning.
                        _logger.LogWarning($"""
                            Failed to deserialize previously unseen configuration file.
                            Configuration file path: '{args.ConfigurationFilePath}'
                            """);
                    }
                }

                break;

            case RazorFileChangeKind.Removed:
                {
                    if (_projectManager.TryGetLoadedProject(projectKey, out _))
                    {
                        _logger.LogInformation($"""
                            Configuration file removed for project '{projectKey}'.
                            Configuration file path: '{args.ConfigurationFilePath}'
                            """);

                        _workQueue.AddWork(new ResetProject(projectKey));
                    }
                    else
                    {
                        _logger.LogWarning($"""
                            Failed to resolve associated project on configuration removed event.
                            Configuration file path: '{args.ConfigurationFilePath}'
                            """);
                    }
                }

                break;
        }
    }

    private void AddProject(ProjectKey projectKey, RazorProjectInfo projectInfo, CancellationToken token)
    {
        // We fire-and-forget the call to AddProjectAsync below, which is OK because the operation will be
        // enqueued on the ProjectSnapshotManager's dispatcher. So, it will occur before the work added
        // below updates the project, since that also happens on the dispatcher.
        _projectService
            .AddProjectAsync(
                filePath: FilePathNormalizer.Normalize(projectInfo.FilePath),
                intermediateOutputPath: projectKey.Id,
                projectInfo.Configuration,
                projectInfo.RootNamespace,
                projectInfo.DisplayName,
                token)
            .Forget();

        _logger.LogInformation($"Added {projectKey.Id}.");

        _workQueue.AddWork(new UpdateProject(projectKey, projectInfo));
    }
}
