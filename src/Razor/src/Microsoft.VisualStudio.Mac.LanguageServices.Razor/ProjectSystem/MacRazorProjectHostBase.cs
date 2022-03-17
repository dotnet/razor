﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Threading;
using MonoDevelop.Projects;

namespace Microsoft.VisualStudio.Mac.LanguageServices.Razor.ProjectSystem
{
    internal abstract class MacRazorProjectHostBase
    {
        // References changes are always triggered when project changes happen.
        private const string ProjectChangedHint = "References";

        private bool _batchingProjectChanges;
        private readonly ProjectSnapshotManagerBase _projectSnapshotManager;
        private readonly AsyncSemaphore _onProjectChangedInnerSemaphore;
        private readonly AsyncSemaphore _projectChangedSemaphore;
        private readonly Dictionary<string, HostDocument> _currentDocuments;

        public MacRazorProjectHostBase(
            DotNetProject project!!,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!,
            ProjectSnapshotManagerBase projectSnapshotManager!!)
        {
            DotNetProject = project;
            ProjectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _projectSnapshotManager = projectSnapshotManager;
            _onProjectChangedInnerSemaphore = new AsyncSemaphore(initialCount: 1);
            _projectChangedSemaphore = new AsyncSemaphore(initialCount: 1);
            _currentDocuments = new Dictionary<string, HostDocument>(FilePathComparer.Instance);

            AttachToProject();
        }

        public DotNetProject DotNetProject { get; }

        public HostProject HostProject { get; private set; }

        protected ProjectSnapshotManagerDispatcher ProjectSnapshotManagerDispatcher { get; }

        public void Detach()
        {
            ProjectSnapshotManagerDispatcher.AssertDispatcherThread();

            DotNetProject.Modified -= DotNetProject_Modified;

            UpdateHostProjectProjectSnapshotManagerDispatcher(null);
        }

        protected abstract Task OnProjectChangedAsync();

        // Protected virtual for testing
        protected virtual void AttachToProject()
        {
            ProjectSnapshotManagerDispatcher.AssertDispatcherThread();

            DotNetProject.Modified += DotNetProject_Modified;

            // Trigger the initial update to the project.
            _batchingProjectChanges = true;
            _ = Task.Factory.StartNew(ProjectChangedBackgroundAsync, null, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
        }

        // Must be called inside the lock.
        protected async Task UpdateHostProjectUnsafeAsync(HostProject newHostProject)
        {
            await ProjectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () => UpdateHostProjectProjectSnapshotManagerDispatcher(newHostProject), CancellationToken.None).ConfigureAwait(false);
        }

        protected async Task ExecuteWithLockAsync(Func<Task> func)
        {
            using (await _projectChangedSemaphore.EnterAsync().ConfigureAwait(false))
            {
                await func().ConfigureAwait(false);
            }
        }

        private async Task ProjectChangedBackgroundAsync(object state)
        {
            _batchingProjectChanges = false;

            // Ensure ordering, typically we'll only have 1 background thread in flight at a time. However,
            // between this line and the one prior another background thread could have also entered this
            // method. This is here to protect against us changing the order of project changed events.
            using (await _onProjectChangedInnerSemaphore.EnterAsync().ConfigureAwait(false))
            {
                await OnProjectChangedAsync();
            }
        }

        private void DotNetProject_Modified(object sender, SolutionItemModifiedEventArgs args!!)
        {
            _ = ProjectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync((args, ct) =>
            {
                if (_batchingProjectChanges)
                {
                    // Already waiting to recompute host project, no need to do any more work to determine if we're dirty.
                    return;
                }

                var projectChanged = args.Any(arg => string.Equals(arg.Hint, ProjectChangedHint, StringComparison.Ordinal));
                if (projectChanged)
                {
                    // This method can be spammed for tons of project change events but all we really care about is "are we dirty?".
                    // Therefore, we re-dispatch here to allow any remaining project change events to fire and to then only have 1 host
                    // project change trigger; this way we don't spam our own system with re-configure calls.
                    _batchingProjectChanges = true;
                    _ = Task.Factory.StartNew(ProjectChangedBackgroundAsync, null, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                }
            }, args, CancellationToken.None);
        }

        private void UpdateHostProjectProjectSnapshotManagerDispatcher(object state)
        {
            ProjectSnapshotManagerDispatcher.AssertDispatcherThread();

            var newHostProject = (HostProject)state;

            if (HostProject is null && newHostProject is null)
            {
                // This is a no-op. This project isn't using Razor.
            }
            else if (HostProject is null && newHostProject != null)
            {
                _projectSnapshotManager.ProjectAdded(newHostProject);
            }
            else if (HostProject != null && newHostProject is null)
            {
                _projectSnapshotManager.ProjectRemoved(HostProject);
            }
            else
            {
                _projectSnapshotManager.ProjectConfigurationChanged(newHostProject);
            }

            HostProject = newHostProject;
        }

        protected void AddDocument(HostProject hostProject, string filePath, string relativeFilePath)
        {
            ProjectSnapshotManagerDispatcher.AssertDispatcherThread();

            if (_currentDocuments.ContainsKey(filePath))
            {
                return;
            }

            var hostDocument = new HostDocument(filePath, relativeFilePath);
            _projectSnapshotManager.DocumentAdded(hostProject, hostDocument, new FileTextLoader(filePath, defaultEncoding: null));

            _currentDocuments[filePath] = hostDocument;
        }

        protected void RemoveDocument(HostProject hostProject, string filePath)
        {
            if (_currentDocuments.TryGetValue(filePath, out var hostDocument))
            {
                _projectSnapshotManager.DocumentRemoved(hostProject, hostDocument);
                _currentDocuments.Remove(filePath);
            }
        }
    }
}
