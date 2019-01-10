// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Text;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Projects;
using Workspace = Microsoft.CodeAnalysis.Workspace;

namespace Microsoft.VisualStudio.Mac.LanguageServices.Razor
{
    [System.Composition.Shared]
    [Export(typeof(VisualStudioMacWorkspaceAccessor))]
    internal class DefaultVisualStudioMacWorkspaceAccessor : VisualStudioMacWorkspaceAccessor
    {
        private readonly TextBufferProjectService _projectService;

        [ImportingConstructor]
        public DefaultVisualStudioMacWorkspaceAccessor(TextBufferProjectService projectService)
        {
            if (projectService == null)
            {
                throw new ArgumentNullException(nameof(projectService));
            }

            _projectService = projectService;
        }

        public override bool TryGetWorkspace(Solution solution, out Workspace workspace)
        {
            if (solution == null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            workspace = TypeSystemService.GetWorkspace(solution);

            // Workspace cannot be null at this point. If TypeSystemService.GetWorkspace isn't able to find a corresponding
            // workspace it returns an empty workspace. Therefore, in order to see if we have a valid workspace we need to
            // cross-check it against the list of active non-empty workspaces.

            if (!TypeSystemService.AllWorkspaces.Contains(workspace))
            {
                // We were returned the empty workspace which is equivalent to us not finding a valid workspace for our text buffer.
                workspace = null;
                return false;
            }

            return true;
        }
    }
}
