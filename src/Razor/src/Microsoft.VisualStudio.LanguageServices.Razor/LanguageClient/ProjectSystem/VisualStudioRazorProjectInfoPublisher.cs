// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Razor.LanguageClient.ProjectSystem;

[Export(typeof(IRazorStartupService)]
[Export(typeof(IRazorProjectInfoPublisher))]
internal sealed class VisualStudioRazorProjectInfoPublisher : CodeAnalysis.Razor.ProjectSystem.RazorProjectInfoPublisher, IRazorStartupService
{
    [ImportingConstructor]
    public VisualStudioRazorProjectInfoPublisher(IProjectSnapshotManager projectManager)
        : base()
    {
        projectManager.Changed += ProjectManager_Changed;
    }

    private void ProjectManager_Changed(object sender, ProjectChangeEventArgs e)
    {
        // Don't do any work if the solution is closing
        if (e.SolutionIsClosing)
        {
            return;
        }

        switch (e.Kind)
        {
            case ProjectChangeKind.ProjectAdded:
            case ProjectChangeKind.ProjectChanged:
            case ProjectChangeKind.DocumentRemoved:
            case ProjectChangeKind.DocumentAdded:
                var newer = e.Newer.AssumeNotNull();
                AddWork(newer.ToRazorProjectInfo());
                break;

            case ProjectChangeKind.ProjectRemoved:
                var older = e.Older.AssumeNotNull();
                AddWork(new RazorProjectInfo(
                    older.Key,
                    older.FilePath,
                    configuration: FallbackRazorConfiguration.Latest,
                    rootNamespace: null,
                    displayName: "",
                    projectWorkspaceState: ProjectWorkspaceState.Default,
                    documents: []));

                break;

            case ProjectChangeKind.DocumentChanged:
                break;

            default:
                throw new NotSupportedException($"Unsupported {nameof(ProjectChangeKind)}: {e.Kind}");
        }
    }
}
