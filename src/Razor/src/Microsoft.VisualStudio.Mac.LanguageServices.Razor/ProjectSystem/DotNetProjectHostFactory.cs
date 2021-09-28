// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor;
using MonoDevelop.Projects;

namespace Microsoft.VisualStudio.Mac.LanguageServices.Razor.ProjectSystem
{
    [System.Composition.Shared]
    [Export(typeof(DotNetProjectHostFactory))]
    internal class DotNetProjectHostFactory
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly VisualStudioMacWorkspaceAccessor _workspaceAccessor;
        private readonly TextBufferProjectService _projectService;

        [ImportingConstructor]
        public DotNetProjectHostFactory(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            VisualStudioMacWorkspaceAccessor workspaceAccessor,
            TextBufferProjectService projectService)
        {
            if (projectSnapshotManagerDispatcher == null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (workspaceAccessor == null)
            {
                throw new ArgumentNullException(nameof(workspaceAccessor));
            }

            if (projectService == null)
            {
                throw new ArgumentNullException(nameof(projectService));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _workspaceAccessor = workspaceAccessor;
            _projectService = projectService;
        }

        public DotNetProjectHost Create(DotNetProject project)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            var projectHost = new DefaultDotNetProjectHost(project, _projectSnapshotManagerDispatcher, _workspaceAccessor, _projectService);
            return projectHost;
        }
    }
}
