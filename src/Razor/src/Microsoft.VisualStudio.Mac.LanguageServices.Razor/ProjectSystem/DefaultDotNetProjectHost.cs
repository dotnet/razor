// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Editor.Razor;
using MonoDevelop.Projects;

namespace Microsoft.VisualStudio.Mac.LanguageServices.Razor.ProjectSystem
{
    internal class DefaultDotNetProjectHost : DotNetProjectHost
    {
        private const string ExplicitRazorConfigurationCapability = "DotNetCoreRazorConfiguration";

        private readonly DotNetProject _project;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly VisualStudioMacWorkspaceAccessor _workspaceAccessor;
        private readonly TextBufferProjectService _projectService;
        private RazorProjectHostBase _razorProjectHost;

        public DefaultDotNetProjectHost(
            DotNetProject project!!,
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!,
            VisualStudioMacWorkspaceAccessor workspaceAccessor!!,
            TextBufferProjectService projectService!!)
        {
            _project = project;
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _workspaceAccessor = workspaceAccessor;
            _projectService = projectService;
        }

        // Internal for testing
        internal DefaultDotNetProjectHost(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!,
            VisualStudioMacWorkspaceAccessor workspaceAccessor!!,
            TextBufferProjectService projectService!!)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _workspaceAccessor = workspaceAccessor;
            _projectService = projectService;
        }

        public override DotNetProject Project => _project;

        public override void Subscribe()
        {
            UpdateRazorHostProject();

            _project.ProjectCapabilitiesChanged += Project_ProjectCapabilitiesChanged;
            _project.Disposing += Project_Disposing;
        }

        private void Project_Disposing(object sender, EventArgs e)
        {
            _project.ProjectCapabilitiesChanged -= Project_ProjectCapabilitiesChanged;
            _project.Disposing -= Project_Disposing;

            _ = _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() => DetachCurrentRazorProjectHost(), CancellationToken.None);
        }

        private void Project_ProjectCapabilitiesChanged(object sender, EventArgs e) => UpdateRazorHostProject();

        // Internal for testing
        internal void UpdateRazorHostProject()
        {
            _ = _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                DetachCurrentRazorProjectHost();

                if (!_projectService.IsSupportedProject(_project))
                {
                    // Not a Razor compatible project.
                    return;
                }

                if (!TryGetProjectSnapshotManager(out var projectSnapshotManager))
                {
                    // Could not get a ProjectSnapshotManager for the current project.
                    return;
                }

                if (_project.IsCapabilityMatch(ExplicitRazorConfigurationCapability))
                {
                    // SDK >= 2.1
                    _razorProjectHost = new DefaultRazorProjectHost(_project, _projectSnapshotManagerDispatcher, projectSnapshotManager);
                    return;
                }

                // We're an older version of Razor at this point, SDK < 2.1
                _razorProjectHost = new FallbackRazorProjectHost(_project, _projectSnapshotManagerDispatcher, projectSnapshotManager);
            }, CancellationToken.None);
        }

        private bool TryGetProjectSnapshotManager(out ProjectSnapshotManagerBase projectSnapshotManagerBase)
        {
            if (!_workspaceAccessor.TryGetWorkspace(_project.ParentSolution, out var workspace))
            {
                // Could not locate workspace for razor project. Project is most likely tearing down.
                projectSnapshotManagerBase = null;
                return false;
            }

            var languageService = workspace.Services.GetLanguageServices(RazorLanguage.Name);
            projectSnapshotManagerBase = (ProjectSnapshotManagerBase)languageService.GetRequiredService<ProjectSnapshotManager>();

            return true;
        }

        private void DetachCurrentRazorProjectHost()
        {
            _razorProjectHost?.Detach();
            _razorProjectHost = null;
        }
    }
}
