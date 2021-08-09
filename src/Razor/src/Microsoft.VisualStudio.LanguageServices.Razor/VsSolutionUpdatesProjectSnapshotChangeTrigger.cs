// Copyright (c) .NET Foundation. All rights reserved.
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
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    [Export(typeof(ProjectSnapshotChangeTrigger))]
    internal class VsSolutionUpdatesProjectSnapshotChangeTrigger : ProjectSnapshotChangeTrigger, IVsUpdateSolutionEvents2
    {
        private readonly IServiceProvider _services;
        private readonly TextBufferProjectService _projectService;
        private readonly ProjectWorkspaceStateGenerator _workspaceStateGenerator;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private ProjectSnapshotManagerBase _projectManager;
        private readonly JoinableTaskContext _joinableTaskContext;

        [ImportingConstructor]
        public VsSolutionUpdatesProjectSnapshotChangeTrigger(
            [Import(typeof(SVsServiceProvider))] IServiceProvider services,
            TextBufferProjectService projectService,
            ProjectWorkspaceStateGenerator workspaceStateGenerator,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            JoinableTaskContext joinableTaskContext)
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

            if (projectSnapshotManagerDispatcher is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            _services = services;
            _projectService = projectService;
            _workspaceStateGenerator = workspaceStateGenerator;
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _joinableTaskContext = joinableTaskContext;
        }

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            _projectManager = projectManager;

            _ = _joinableTaskContext.Factory.RunAsync(async () =>
            {
                await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

                // Attach the event sink to solution update events.
                if (_services.GetService(typeof(SVsSolutionBuildManager)) is IVsSolutionBuildManager solutionBuildManager)
                {
                    // We expect this to be called only once. So we don't need to Unadvise.
                    var hr = solutionBuildManager.AdviseUpdateSolutionEvents(this, out _);
                    Marshal.ThrowExceptionForHR(hr);
                }
            });
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
            _ = OnProjectBuiltAsync(pHierProj, CancellationToken.None);

            return VSConstants.S_OK;
        }

        // Internal for testing
        internal Task OnProjectBuiltAsync(IVsHierarchy projectHierarchy, CancellationToken cancellationToken)
        {
            var projectFilePath = _projectService.GetProjectPath(projectHierarchy);
            return _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                var projectSnapshot = _projectManager.GetLoadedProject(projectFilePath);
                if (projectSnapshot != null)
                {
                    var workspaceProject = _projectManager.Workspace.CurrentSolution.Projects.FirstOrDefault(
                        wp => FilePathComparer.Instance.Equals(wp.FilePath, projectSnapshot.FilePath));
                    if (workspaceProject != null)
                    {
                        // Trigger a tag helper update by forcing the project manager to see the workspace Project
                        // from the current solution.
                        _workspaceStateGenerator.Update(workspaceProject, projectSnapshot, cancellationToken);
                    }
                }
            }, cancellationToken);
        }
    }
}
