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

/// <summary>
///  <para>
///  This service has complex and potentially flaky(!) semantics. When notified of project configuration file
///  adds, removes, and changes, it works to synchronize those changes with the <see cref="IRazorProjectService"/>.
///  This is handled like so:
///  </para>
///
///  <list type="number">
///   <item>
///   When a configuration file is added, an attempt is made to add the project to the
///   <see cref="IRazorProjectService"/>. This might ultimately be a no-op if the project already exists,
///   but it'll return the <see cref="ProjectKey"/> in either case. This project key is added to
///   <see cref="_configurationFileToProjectKeyMap"/> and used to enqueue an update to the project with the
///   <see cref="RazorProjectInfo"/> that was deserialized from the configuration file.
///   </item>
///
///   <item>
///   When a configuration file is changed, <see cref="_configurationFileToProjectKeyMap"/> is used to
///   retrieve the <see cref="ProjectKey"/>. This key is then used to enqueue an update to the project
///   the deserialized <see cref="RazorProjectInfo"/>.
///   </item>
///
///   <item>
///   When a configuration file is removed, it is removed from <see cref="_configurationFileToProjectKeyMap"/>
///   and its <see cref="ProjectKey"/> (if we knew about it) will be used to enqueue an update to reset the project
///   to an empty, unconfigured state.
///   </item>
///  </list>
///
///  <para>
///  Project updates are processed after a delay. The machinery ensures, that if an update for a project is already
///  in flight when a new update is enqueued, the project info will be replaced with the latest. This debouncing
///  accounts for scenarios where the configuration file is updated in rapid succession.
///  </para>
/// </summary>
internal partial class ProjectConfigurationStateSynchronizer : IProjectConfigurationFileChangeListener, IDisposable
{
    private static readonly TimeSpan s_delay = TimeSpan.FromMilliseconds(250);

    private readonly IRazorProjectService _projectService;
    private readonly LanguageServerFeatureOptions _options;
    private readonly ILogger _logger;
    private readonly TimeSpan _delay;

    private readonly CancellationTokenSource _disposeTokenSource;
    private readonly Dictionary<ProjectKey, UpdateItem> _projectUpdates = [];

    private ImmutableDictionary<string, ProjectKey> _configurationFileToProjectKeyMap =
        ImmutableDictionary<string, ProjectKey>.Empty.WithComparers(keyComparer: FilePathComparer.Instance);

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
        _delay = delay;

        _disposeTokenSource = new();
    }

    public void Dispose()
    {
        _disposeTokenSource.Cancel();
        _disposeTokenSource.Dispose();
    }

    public void ProjectConfigurationFileChanged(ProjectConfigurationFileChangeEventArgs args)
    {
        var configurationFilePath = FilePathNormalizer.Normalize(args.ConfigurationFilePath);

        switch (args.Kind)
        {
            case RazorFileChangeKind.Changed:
                {
                    if (args.TryDeserialize(_options, out var projectInfo))
                    {
                        if (_configurationFileToProjectKeyMap.TryGetValue(configurationFilePath, out var projectKey))
                        {
                            _logger.LogInformation($"""
                                Configuration file changed for project '{projectKey.Id}'.
                                Configuration file path: '{configurationFilePath}'
                                """);

                            EnqueueProjectUpdate(projectKey, projectInfo, _disposeTokenSource.Token);
                        }
                        else
                        {
                            _logger.LogWarning($"""
                                Adding project for previously unseen configuration file.
                                Configuration file path: '{configurationFilePath}'
                                """);

                            AddProject(configurationFilePath, projectInfo, _disposeTokenSource.Token);
                        }
                    }
                    else
                    {
                        if (_configurationFileToProjectKeyMap.TryGetValue(configurationFilePath, out var projectKey))
                        {
                            _logger.LogWarning($"""
                                Failed to deserialize after change to configuration file for project '{projectKey.Id}'.
                                Configuration file path: '{configurationFilePath}'
                                """);

                            // We found the last associated project file for the configuration file. Reset the project since we can't
                            // accurately determine its configurations.

                            EnqueueProjectReset(projectKey, _disposeTokenSource.Token);
                        }
                        else
                        {
                            // Could not resolve an associated project file.
                            _logger.LogWarning($"""
                                Failed to deserialize after change to previously unseen configuration file.
                                Configuration file path: '{configurationFilePath}'
                                """);
                        }
                    }
                }

                break;

            case RazorFileChangeKind.Added:
                {
                    if (args.TryDeserialize(_options, out var projectInfo))
                    {
                        AddProject(configurationFilePath, projectInfo, _disposeTokenSource.Token);
                    }
                    else
                    {
                        // This is the first time we've seen this configuration file, but we can't deserialize it.
                        // The only thing we can really do is issue a warning.
                        _logger.LogWarning($"""
                            Failed to deserialize previously unseen configuration file.
                            Configuration file path: '{configurationFilePath}'
                            """);
                    }
                }

                break;

            case RazorFileChangeKind.Removed:
                {
                    if (ImmutableInterlocked.TryRemove(ref _configurationFileToProjectKeyMap, configurationFilePath, out var projectKey))
                    {
                        _logger.LogInformation($"""
                            Configuration file removed for project '{projectKey}'.
                            Configuration file path: '{configurationFilePath}'
                            """);

                        EnqueueProjectReset(projectKey, _disposeTokenSource.Token);
                    }
                    else
                    {
                        _logger.LogWarning($"""
                            Failed to resolve associated project on configuration removed event.
                            Configuration file path: '{configurationFilePath}'
                            """);
                    }
                }

                break;
        }
    }

    private void AddProject(string configurationFilePath, RazorProjectInfo projectInfo, CancellationToken cancellationToken)
    {
        // Note that we fire-and-forget adding the project here because it's an asynchronous operation.
        // This means that the add will not happen immediately. However, once it does, we'll enqueue a project update.
        // This *could* represent a race condition if another *newer* project update is enqueued after the project is
        // added but before the project update for the add is enqueued.
        AddProjectAsync(configurationFilePath, projectInfo, cancellationToken).Forget();

        async Task AddProjectAsync(string configurationFilePath, RazorProjectInfo projectInfo, CancellationToken cancellationToken)
        {
            var projectFilePath = FilePathNormalizer.Normalize(projectInfo.FilePath);
            var intermediateOutputPath = FilePathNormalizer.GetNormalizedDirectoryName(configurationFilePath);

            var projectKey = await _projectService
                .AddProjectAsync(
                    projectFilePath,
                    intermediateOutputPath,
                    projectInfo.Configuration,
                    projectInfo.RootNamespace,
                    projectInfo.DisplayName,
                    cancellationToken)
                .ConfigureAwait(false);

            ImmutableInterlocked.AddOrUpdate(ref _configurationFileToProjectKeyMap, configurationFilePath, projectKey, static (k, v) => v);
            _logger.LogInformation($"Project configuration file added for project '{projectFilePath}': '{configurationFilePath}'");

            EnqueueProjectUpdate(projectKey, projectInfo, cancellationToken);
        }
    }

    private void EnqueueProjectReset(ProjectKey projectKey, CancellationToken cancellationToken)
        => EnqueueProjectUpdate(projectKey, projectInfo: null, cancellationToken);

    private void EnqueueProjectUpdate(ProjectKey projectKey, RazorProjectInfo? projectInfo, CancellationToken cancellationToken)
    {
        // Note: We lock on _projectUpdates to protect access to both _projectUpdates and UpdateItem.
        lock (_projectUpdates)
        {
            if (!_projectUpdates.TryGetValue(projectKey, out var updateItem))
            {
                updateItem = new(projectKey);
                _projectUpdates.Add(projectKey, updateItem);
            }

            // Ensure that we use the most recent project info, which could be null
            // in the case of a reset.
            updateItem.ProjectInfo = projectInfo;

            // If this is new or its task has completed, start a new task to update
            // the project after a delay. Note that we don't provide the project info
            // because it might be updated before the delay completes.
            if (updateItem.Task is null || updateItem.Task.IsCompleted)
            {
                updateItem.Task = UpdateAfterDelayAsync(projectKey, cancellationToken);
            }
        }

        async Task UpdateAfterDelayAsync(ProjectKey projectKey, CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);

            // First, retrieve the project info (if any) from the update item.
            RazorProjectInfo? projectInfo;

            lock (_projectUpdates)
            {
                if (!_projectUpdates.TryGetValue(projectKey, out var updateItem))
                {
                    // The update item should not be removed before we've handled it here.
                    Assumed.Unreachable();
                    return;
                }

                projectInfo = updateItem.ProjectInfo;
            }

            await UpdateProjectAsync(projectKey, projectInfo, cancellationToken).ConfigureAwait(false);

            // Now that the project update is complete, we can remove the update item, but
            // be sure to protect access.
            lock (_projectUpdates)
            {
                _projectUpdates.Remove(projectKey);
            }

            Task UpdateProjectAsync(ProjectKey projectKey, RazorProjectInfo? projectInfo, CancellationToken cancellationToken)
            {
                if (projectInfo is null)
                {
                    // When we're passed a null RazorProjectInfo, we reset the project back to an empty, unconfigured state.
                    return _projectService.UpdateProjectAsync(
                        projectKey,
                        configuration: null,
                        rootNamespace: null,
                        displayName: "",
                        ProjectWorkspaceState.Default,
                        documents: [],
                        cancellationToken);
                }

                _logger.LogInformation($"Updating {projectKey} with real project info.");

                return _projectService.UpdateProjectAsync(
                    projectKey,
                    projectInfo.Configuration,
                    projectInfo.RootNamespace,
                    projectInfo.DisplayName,
                    projectInfo.ProjectWorkspaceState,
                    projectInfo.Documents,
                    cancellationToken);
            }
        }
    }

    /// <summary>
    /// This is used to track the running <see cref="System.Threading.Tasks.Task"/> that
    /// ultimately updates the project and the latest <see cref="RazorProjectInfo"/> that
    /// the project will be updated with.
    /// </summary>
    private sealed record UpdateItem(ProjectKey Key)
    {
        public Task? Task { get; set; }
        public RazorProjectInfo? ProjectInfo { get; set; }
    }
}
