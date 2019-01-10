// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.Editor.Razor
{
    [System.Composition.Shared]
    [Export(typeof(WorkspaceStateFactory))]
    internal class DefaultWorkspaceStateFactory : WorkspaceStateFactory
    {
        public override WorkspaceState Create(Workspace workspace)
        {
            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            var languageServices = workspace.Services.GetLanguageServices(RazorLanguage.Name);
            var projectSnapshotManager = languageServices.GetRequiredService<ProjectSnapshotManager>();
            var importDocumentManager = languageServices.GetRequiredService<ImportDocumentManager>();
            var workspaceState = new DefaultWorkspaceState(
                workspace,
                projectSnapshotManager,
                importDocumentManager);

            return workspaceState;
        }
    }
}
