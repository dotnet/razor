// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.RoslynWorkspace;

public class RazorWorkspaceListener : IDisposable
{
    private static readonly TimeSpan s_debounceTime = TimeSpan.FromMilliseconds(100);

    private string? _projectRazorJsonFileName;
    private readonly Dictionary<string, TaskDelayScheduler> _workQueues;
    private readonly object _gate = new();

    public RazorWorkspaceListener()
    {
        var comparer = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;
        _workQueues = new Dictionary<string, TaskDelayScheduler>(comparer);
    }

    public void EnsureInitialized(Workspace workspace, string projectRazorJsonFileName)
    {
        // Make sure we don't hook up the event handler multiple times
        if (_projectRazorJsonFileName is not null)
        {
            return;
        }

        _projectRazorJsonFileName = projectRazorJsonFileName;
        workspace.WorkspaceChanged += Workspace_WorkspaceChanged;
    }

    private void Workspace_WorkspaceChanged(object? sender, WorkspaceChangeEventArgs e)
    {
        switch (e.Kind)
        {
            case WorkspaceChangeKind.SolutionAdded:
            case WorkspaceChangeKind.SolutionChanged:
            case WorkspaceChangeKind.SolutionReloaded:
                foreach (var project in e.NewSolution.Projects)
                {
                    EnqueueUpdate(project);
                }

                break;
            case WorkspaceChangeKind.ProjectRemoved:
                RemoveProject(e.OldSolution.GetProject(e.ProjectId));
                break;
            case WorkspaceChangeKind.ProjectAdded:
            case WorkspaceChangeKind.ProjectChanged:
            case WorkspaceChangeKind.ProjectReloaded:
            case WorkspaceChangeKind.DocumentAdded:
            case WorkspaceChangeKind.DocumentRemoved:
            case WorkspaceChangeKind.DocumentReloaded:
            case WorkspaceChangeKind.DocumentChanged:
            case WorkspaceChangeKind.AdditionalDocumentAdded:
            case WorkspaceChangeKind.AdditionalDocumentRemoved:
            case WorkspaceChangeKind.AdditionalDocumentReloaded:
            case WorkspaceChangeKind.AdditionalDocumentChanged:
            case WorkspaceChangeKind.DocumentInfoChanged:
            case WorkspaceChangeKind.AnalyzerConfigDocumentAdded:
            case WorkspaceChangeKind.AnalyzerConfigDocumentRemoved:
            case WorkspaceChangeKind.AnalyzerConfigDocumentReloaded:
            case WorkspaceChangeKind.AnalyzerConfigDocumentChanged:
                EnqueueUpdate(e.NewSolution.GetProject(e.ProjectId));
                break;
            case WorkspaceChangeKind.SolutionCleared:
            case WorkspaceChangeKind.SolutionRemoved:
                foreach (var project in e.OldSolution.Projects)
                {
                    RemoveProject(project);
                }

                break;
            default:
                break;
        }
    }

    private void RemoveProject(Project? project)
    {
        if (project?.FilePath is null)
        {
            return;
        }

        TaskDelayScheduler? scheduler;
        lock (_gate)
        {
            if (_workQueues.TryGetValue(project.FilePath, out scheduler))
            {
                _workQueues.Remove(project.FilePath);
            }
        }

        scheduler?.Dispose();
    }

    private void EnqueueUpdate(Project? project)
    {
        if (_projectRazorJsonFileName is null ||
            project is not
            {
                FilePath: not null,
                Language: LanguageNames.CSharp
            })
        {
            return;
        }

        TaskDelayScheduler? scheduler;
        lock (_gate)
        {
            if (!_workQueues.TryGetValue(project.FilePath, out scheduler))
            {
                scheduler = new TaskDelayScheduler(s_debounceTime, CancellationToken.None);
            }
        }

        scheduler.ScheduleAsyncTask(ct => RazorProjectJsonSerializer.SerializeAsync(project, _projectRazorJsonFileName, ct), CancellationToken.None);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var (_, value) in _workQueues)
            {
                value.Dispose();
            }
        }
    }
}
