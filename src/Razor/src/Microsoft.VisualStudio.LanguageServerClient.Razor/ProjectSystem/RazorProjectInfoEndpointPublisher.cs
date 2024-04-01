// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Utilities;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.ProjectSystem;

/// <summary>
/// Publishes project data (including TagHelper info) discovered OOB to the server via LSP notification
/// instead of old method of writing a project configuration bin file
/// </summary>
[Shared]
[Export(typeof(RazorProjectInfoEndpointPublisher))]
internal partial class RazorProjectInfoEndpointPublisher
{
    private readonly LSPRequestInvoker _requestInvoker;
    private readonly IProjectSnapshotManager _projectManager;

    private readonly AsyncBatchingWorkQueue<(IProjectSnapshot Project, bool Removal)> _workQueue;

    private bool _active;

    [ImportingConstructor]
    public RazorProjectInfoEndpointPublisher(
        LSPRequestInvoker requestInvoker,
        IProjectSnapshotManager projectManager
    )
    {
        _requestInvoker = requestInvoker;
        _projectManager = projectManager;

        _workQueue = new(
            EnqueueDelay,
            ProcessBatchAsync,
            CancellationToken.None);
    }

    // internal for testing
    // Delay between publishes to prevent bursts of changes yet still be responsive to changes.
    internal TimeSpan EnqueueDelay { get; set; } = TimeSpan.FromMilliseconds(250);

    public void StartSending()
    {
        _active = true;

        _projectManager.Changed += ProjectManager_Changed;

        var projects = _projectManager.GetProjects();
        foreach (var project in projects)
        {
            if (ProjectWorkspacePublishable(project))
            {
                // Don't enqueue project addition as those are unlikely to come through in large batches.
                // Also ensures that we won't get project removal go through the queue without project addition.
                ImmediatePublish(project);
            }
        }
    }

    private void ProjectManager_Changed(object sender, ProjectChangeEventArgs args)
    {
        // Don't do any work if the solution is closing
        if (args.SolutionIsClosing)
        {
            return;
        }

        switch (args.Kind)
        {
            case ProjectChangeKind.ProjectChanged:
            case ProjectChangeKind.DocumentRemoved:
            case ProjectChangeKind.DocumentAdded:
                if (!ProjectWorkspacePublishable(args.Newer))
                {
                    break;
                }

                EnqueuePublish(args.Newer!);

                break;

            case ProjectChangeKind.ProjectAdded:

                if (ProjectWorkspacePublishable(args.Newer))
                {
                    // Don't enqueue project addition as those are unlikely to come through in large batches.
                    // Also ensures that we won't get project removal go through the queue without project addition.
                    ImmediatePublish(args.Newer!);
                }

                break;

            case ProjectChangeKind.ProjectRemoved:
                // Enqueue removal so it will replace any other project changes in the work queue as they unnecessary now.
                EnqueueRemoval(args.Older!);
                break;

            default:
                Debug.Fail("A new ProjectChangeKind has been added that the RazorProjectInfoEndpointPublisher doesn't know how to deal with");
                break;
        }
    }

    // We want to wait until our project has been initialized (ProjectWorkspaceState != null)
    // so that we don't publish half-finished projects, which can cause things like Semantic coloring to "flash"
    // when they update repeatedly as they load.
    private static bool ProjectWorkspacePublishable(IProjectSnapshot? project)
    {
        return project?.ProjectWorkspaceState != null;
    }

    private void EnqueuePublish(IProjectSnapshot projectSnapshot)
    {
        _workQueue.AddWork((Project: projectSnapshot, Removal: false));
    }

    private void EnqueueRemoval(IProjectSnapshot projectSnapshot)
    {
        _workQueue.AddWork((Project: projectSnapshot, Removal: true));
    }

    private ValueTask ProcessBatchAsync(ImmutableArray<(IProjectSnapshot Project, bool Removal)> workItems, CancellationToken token)
    {
        foreach (var workItem in workItems.GetMostRecentUniqueItems(Comparer.Instance))
        {
            if (token.IsCancellationRequested)
            {
                return default;
            }

            if (workItem.Removal)
            {
                RemovePublishingData(workItem.Project);
            }
            else
            {
                ImmediatePublish(workItem.Project);
            }
        }

        return default;
    }

    private void RemovePublishingData(IProjectSnapshot projectSnapshot)
    {
        if (_active)
        {
            ImmediatePublish(projectSnapshot.Key, encodedProjectInfo: null);
        }
    }

    private void ImmediatePublish(IProjectSnapshot projectSnapshot)
    {
        if (!_active)
        {
            return;
        }

        using var stream = new MemoryStream();

        var projectInfo = projectSnapshot.ToRazorProjectInfo(projectSnapshot.IntermediateOutputPath);
        projectInfo.SerializeTo(stream);
        var base64ProjectInfo = Convert.ToBase64String(stream.ToArray());

        ImmediatePublish(projectSnapshot.Key, base64ProjectInfo);
    }

    private void ImmediatePublish(ProjectKey projectKey, string? encodedProjectInfo)
    {
        var parameter = new ProjectInfoParams()
        {
            ProjectKeyId = projectKey.Id,
            ProjectInfo = encodedProjectInfo
        };

        _ = _requestInvoker.ReinvokeRequestOnServerAsync<ProjectInfoParams, object>(
                LanguageServerConstants.RazorProjectInfoEndpoint,
                RazorLSPConstants.RazorLanguageServerName,
                parameter,
                CancellationToken.None);
    }
}
