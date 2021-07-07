// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Editor.Razor;
using MonoDevelop.Ide;
using MonoDevelop.Projects;

namespace Microsoft.VisualStudio.Mac.LanguageServices.Razor
{
    [Export(typeof(ProjectSnapshotChangeTrigger))]
    internal class ProjectBuildChangeTrigger : ProjectSnapshotChangeTrigger
    {
        private readonly TextBufferProjectService _projectService;
        private readonly ProjectWorkspaceStateGenerator _workspaceStateGenerator;
        private readonly SingleThreadedDispatcher _singleThreadedDispatcher;
        private ProjectSnapshotManagerBase _projectManager;

        [ImportingConstructor]
        public ProjectBuildChangeTrigger(
            SingleThreadedDispatcher singleThreadedDispatcher, 
            TextBufferProjectService projectService,
            ProjectWorkspaceStateGenerator workspaceStateGenerator)
        {
            if (singleThreadedDispatcher == null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            if (projectService == null)
            {
                throw new ArgumentNullException(nameof(projectService));
            }

            if (workspaceStateGenerator == null)
            {
                throw new ArgumentNullException(nameof(workspaceStateGenerator));
            }

            _singleThreadedDispatcher = singleThreadedDispatcher;
            _projectService = projectService;
            _workspaceStateGenerator = workspaceStateGenerator;
        }

        // Internal for testing
        internal ProjectBuildChangeTrigger(
            SingleThreadedDispatcher singleThreadedDispatcher,
            TextBufferProjectService projectService,
            ProjectWorkspaceStateGenerator workspaceStateGenerator,
            ProjectSnapshotManagerBase projectManager)
        {
            if (singleThreadedDispatcher == null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            if (projectService == null)
            {
                throw new ArgumentNullException(nameof(projectService));
            }

            if (workspaceStateGenerator == null)
            {
                throw new ArgumentNullException(nameof(workspaceStateGenerator));
            }

            if (projectManager == null)
            {
                throw new ArgumentNullException(nameof(projectManager));
            }

            _singleThreadedDispatcher = singleThreadedDispatcher;
            _projectService = projectService;
            _projectManager = projectManager;
            _workspaceStateGenerator = workspaceStateGenerator;
        }

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            if (projectManager == null)
            {
                throw new ArgumentNullException(nameof(projectManager));
            }

            _projectManager = projectManager;

            if (IdeApp.ProjectOperations != null)
            {
                IdeApp.ProjectOperations.EndBuild += ProjectOperations_EndBuild;
            }
        }

        // Internal for testing
        internal void ProjectOperations_EndBuild(object sender, BuildEventArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _singleThreadedDispatcher.AssertDispatcherThread();

            if (!args.Success)
            {
                // Build failed
                return;
            }

            var projectItem = args.SolutionItem;
            if (!_projectService.IsSupportedProject(projectItem))
            {
                // We're hooked into all build events, it's possible to get called with an unsupported project item type.
                return;
            }

            var projectPath = _projectService.GetProjectPath(projectItem);
            var projectSnapshot = _projectManager.GetLoadedProject(projectPath);
            if (projectSnapshot != null)
            {
                var workspaceProject = _projectManager.Workspace.CurrentSolution?.Projects.FirstOrDefault(
                    project => FilePathComparer.Instance.Equals(project.FilePath, projectSnapshot.FilePath));
                if (workspaceProject != null)
                {
                    // Trigger a tag helper update by forcing the project manager to see the workspace Project
                    // from the current solution.
                    _workspaceStateGenerator.Update(workspaceProject, projectSnapshot, CancellationToken.None);
                }
            }
        }
    }
}
