﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    [Export(typeof(ProjectSnapshotChangeTrigger))]
    internal class VsSolutionUpdatesProjectSnapshotChangeTrigger : ProjectSnapshotChangeTrigger, IVsUpdateSolutionEvents2
    {
        private readonly IServiceProvider _services;
        private readonly TextBufferProjectService _projectService;
        private readonly ProjectWorkspaceStateGenerator _workspaceStateGenerator;
        private ProjectSnapshotManagerBase _projectManager;

        [ImportingConstructor]
        public VsSolutionUpdatesProjectSnapshotChangeTrigger(
            [Import(typeof(SVsServiceProvider))] IServiceProvider services,
            TextBufferProjectService projectService,
            ProjectWorkspaceStateGenerator workspaceStateGenerator)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (projectService == null)
            {
                throw new ArgumentNullException(nameof(projectService));
            }

            if (workspaceStateGenerator == null)
            {
                throw new ArgumentNullException(nameof(workspaceStateGenerator));
            }

            _services = services;
            _projectService = projectService;
            _workspaceStateGenerator = workspaceStateGenerator;
        }

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            _projectManager = projectManager;

            // Attach the event sink to solution update events.
            if (_services.GetService(typeof(SVsSolutionBuildManager)) is IVsSolutionBuildManager solutionBuildManager)
            {
                // We expect this to be called only once. So we don't need to Unadvise.
                var hr = solutionBuildManager.AdviseUpdateSolutionEvents(this, out _);
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        public int UpdateSolution_Begin(ref int pfCancelUpdate)
        {
            return VSConstants.S_OK;
        }

        public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand)
        {
            return VSConstants.S_OK;
        }

        public int UpdateSolution_StartUpdate(ref int pfCancelUpdate)
        {
            return VSConstants.S_OK;
        }

        public int UpdateSolution_Cancel()
        {
            return VSConstants.S_OK;
        }

        public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
        {
            return VSConstants.S_OK;
        }

        public int UpdateProjectCfg_Begin(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        // This gets called when the project has finished building.
        public int UpdateProjectCfg_Done(IVsHierarchy pHierProj, IVsCfg pCfgProj, IVsCfg pCfgSln, uint dwAction, int fSuccess, int fCancel)
        {
            var projectPath = _projectService.GetProjectPath(pHierProj);
            var projectSnapshot = _projectManager.GetLoadedProject(projectPath);
            if (projectSnapshot != null)
            {
                var workspaceProject = _projectManager.Workspace.CurrentSolution.Projects.FirstOrDefault(
                    wp => FilePathComparer.Instance.Equals(wp.FilePath, projectSnapshot.FilePath));
                if (workspaceProject != null)
                {
                    // Trigger a tag helper update by forcing the project manager to see the workspace Project
                    // from the current solution.
                    _workspaceStateGenerator.Update(workspaceProject, projectSnapshot, CancellationToken.None);
                }
            }

            return VSConstants.S_OK;
        }
    }
}
