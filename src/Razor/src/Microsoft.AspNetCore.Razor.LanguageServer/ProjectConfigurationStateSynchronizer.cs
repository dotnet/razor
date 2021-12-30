﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Serialization;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class ProjectConfigurationStateSynchronizer : IProjectConfigurationFileChangeListener
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly RazorProjectService _projectService;
        private readonly FilePathNormalizer _filePathNormalizer;
        private readonly ILogger _logger;
        private readonly Dictionary<string, string> _configurationToProjectMap;
        internal readonly Dictionary<string, DelayedProjectInfo> ProjectInfoMap;

        public ProjectConfigurationStateSynchronizer(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            RazorProjectService projectService,
            FilePathNormalizer filePathNormalizer,
            ILoggerFactory loggerFactory)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _projectService = projectService;
            _filePathNormalizer = filePathNormalizer;
            _logger = loggerFactory.CreateLogger<ProjectConfigurationStateSynchronizer>();
            _configurationToProjectMap = new Dictionary<string, string>(FilePathComparer.Instance);
            ProjectInfoMap = new Dictionary<string, DelayedProjectInfo>(FilePathComparer.Instance);
        }

        internal int EnqueueDelay { get; set; } = 250;

        public void ProjectConfigurationFileChanged(ProjectConfigurationFileChangeEventArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _projectSnapshotManagerDispatcher.AssertDispatcherThread();

            switch (args.Kind)
            {
                case RazorFileChangeKind.Changed:
                    {
                        var configurationFilePath = _filePathNormalizer.Normalize(args.ConfigurationFilePath);
                        if (!args.TryDeserialize(out var handle))
                        {
                            if (!_configurationToProjectMap.TryGetValue(configurationFilePath, out var associatedProjectFilePath))
                            {
                                // Could not resolve an associated project file, noop.
                                _logger.LogWarning("Failed to deserialize configuration file after change for an unknown project. Configuration file path: '{0}'", configurationFilePath);
                                return;
                            }
                            else
                            {
                                _logger.LogWarning("Failed to deserialize configuration file after change for project '{0}': '{1}'", associatedProjectFilePath, configurationFilePath);
                            }

                            // We found the last associated project file for the configuration file. Reset the project since we can't
                            // accurately determine its configurations.

                            EnqueueUpdateProject(associatedProjectFilePath, snapshotHandle: null);
                            return;
                        }

                        var projectFilePath = _filePathNormalizer.Normalize(handle.FilePath);
                        _logger.LogInformation("Project configuration file changed for project '{0}': '{1}'", projectFilePath, configurationFilePath);

                        EnqueueUpdateProject(projectFilePath, handle);
                        break;
                    }
                case RazorFileChangeKind.Added:
                    {
                        var configurationFilePath = _filePathNormalizer.Normalize(args.ConfigurationFilePath);
                        if (!args.TryDeserialize(out var handle))
                        {
                            // Given that this is the first time we're seeing this configuration file if we can't deserialize it
                            // then we have to noop.
                            _logger.LogWarning("Failed to deserialize configuration file on configuration added event. Configuration file path: '{0}'", configurationFilePath);
                            return;
                        }

                        var projectFilePath = _filePathNormalizer.Normalize(handle.FilePath);
                        _configurationToProjectMap[configurationFilePath] = projectFilePath;
                        _projectService.AddProject(projectFilePath);

                        _logger.LogInformation("Project configuration file added for project '{0}': '{1}'", projectFilePath, configurationFilePath);
                        EnqueueUpdateProject(projectFilePath, handle);
                        break;
                    }
                case RazorFileChangeKind.Removed:
                    {
                        var configurationFilePath = _filePathNormalizer.Normalize(args.ConfigurationFilePath);
                        if (!_configurationToProjectMap.TryGetValue(configurationFilePath, out var projectFilePath))
                        {
                            // Failed to deserialize the initial handle on add so we can't remove the configuration file because it doesn't exist in the list.
                            _logger.LogWarning("Failed to resolve associated project on configuration removed event. Configuration file path: '{0}'", configurationFilePath);
                            return;
                        }

                        _configurationToProjectMap.Remove(configurationFilePath);

                        _logger.LogInformation("Project configuration file removed for project '{0}': '{1}'", projectFilePath, configurationFilePath);

                        EnqueueUpdateProject(projectFilePath, snapshotHandle: null);
                        break;
                    }
            }

            void UpdateProject(string projectFilePath, FullProjectSnapshotHandle? handle)
            {
                if (projectFilePath is null)
                {
                    throw new ArgumentNullException(nameof(projectFilePath));
                }

                if (handle is null)
                {
                    ResetProject(projectFilePath);
                    return;
                }

                var projectWorkspaceState = handle.ProjectWorkspaceState ?? ProjectWorkspaceState.Default;
                var documents = handle.Documents ?? Array.Empty<DocumentSnapshotHandle>();
                _projectService.UpdateProject(
                    handle.FilePath,
                    handle.Configuration,
                    handle.RootNamespace,
                    projectWorkspaceState,
                    documents);
            }

            async Task UpdateAfterDelayAsync(string projectFilePath)
            {
                await Task.Delay(EnqueueDelay).ConfigureAwait(true);

                var delayedProjectInfo = ProjectInfoMap[projectFilePath];
                UpdateProject(projectFilePath, delayedProjectInfo.FullProjectSnapshotHandle);
            }

            void EnqueueUpdateProject(string projectFilePath, FullProjectSnapshotHandle? snapshotHandle)
            {
                projectFilePath = _filePathNormalizer.Normalize(projectFilePath);
                if (!ProjectInfoMap.ContainsKey(projectFilePath))
                {
                    ProjectInfoMap[projectFilePath] = new DelayedProjectInfo();
                }

                var delayedProjectInfo = ProjectInfoMap[projectFilePath];
                delayedProjectInfo.FullProjectSnapshotHandle = snapshotHandle;

                if (delayedProjectInfo.ProjectUpdateTask is null || delayedProjectInfo.ProjectUpdateTask.IsCompleted)
                {
                    delayedProjectInfo.ProjectUpdateTask = UpdateAfterDelayAsync(projectFilePath);
                }
            }

            void ResetProject(string projectFilePath)
            {
                _projectService.UpdateProject(
                    projectFilePath,
                    configuration: null,
                    rootNamespace: null,
                    ProjectWorkspaceState.Default,
                    Array.Empty<DocumentSnapshotHandle>());
            }
        }

        internal class DelayedProjectInfo
        {
            public Task? ProjectUpdateTask { get; set; }

            public FullProjectSnapshotHandle? FullProjectSnapshotHandle { get; set; }
        }
    }
}
