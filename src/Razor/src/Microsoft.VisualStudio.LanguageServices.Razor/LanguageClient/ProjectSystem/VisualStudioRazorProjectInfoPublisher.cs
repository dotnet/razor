// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor.LanguageClient.ProjectSystem;

[Export(typeof(IRazorProjectInfoPublisher))]
internal sealed class VisualStudioRazorProjectInfoPublisher : CodeAnalysis.Razor.ProjectSystem.RazorProjectInfoPublisher
{
    [ImportingConstructor]
    public VisualStudioRazorProjectInfoPublisher(
        JoinableTaskContext joinableTaskContext,
        LSPEditorFeatureDetector lspEditorFeatureDetector,
        IProjectSnapshotManager projectManager)
        : base()
    {
        var jtf = joinableTaskContext.Factory;

        _ = jtf.RunAsync(async () =>
        {
            // Switch to the main thread because IsLSPEditorAvailable() expects to.
            await jtf.SwitchToMainThreadAsync();

            if (lspEditorFeatureDetector.IsLSPEditorAvailable())
            {
                Initialize(projectManager);
            }
        });
    }

    private void Initialize(IProjectSnapshotManager projectManager)
    {
        projectManager.Changed += ProjectManager_Changed;

        foreach (var project in projectManager.GetProjects())
        {
            EnqueueUpdate(project.ToRazorProjectInfo());
        }
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
                EnqueueUpdate(newer.ToRazorProjectInfo());
                break;

            case ProjectChangeKind.ProjectRemoved:
                var older = e.Older.AssumeNotNull();
                EnqueueRemove(older.Key);
                break;

            case ProjectChangeKind.DocumentChanged:
                break;

            default:
                throw new NotSupportedException($"Unsupported {nameof(ProjectChangeKind)}: {e.Kind}");
        }
    }
}
