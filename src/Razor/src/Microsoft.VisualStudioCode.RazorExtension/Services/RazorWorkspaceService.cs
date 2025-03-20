// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Features;
using Microsoft.Extensions.Logging;

namespace Microsoft.VisualStudioCode.RazorExtension.Services;

[ExportRazorStatelessLspService(typeof(RazorWorkspaceService)), Shared]
[method: ImportingConstructor]
file class WorkspaceService(ILoggerFactory loggerFactory) : RazorWorkspaceService
{
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly ILogger _logger = loggerFactory.CreateLogger<RazorWorkspaceService>();
    private Lock _initializeLock = new();
    private RazorWorkspaceListener? _razorWorkspaceListener;
    private HashSet<ProjectId> _projectIdWithDynamicFiles = [];

    public override void Initialize(Workspace workspace, string pipeName)
    {
        HashSet<ProjectId> projectsToInitialize;
        lock (_initializeLock)
        {
            // Only initialize once
            if (_razorWorkspaceListener is not null)
            {
                return;
            }

            //_logger.LogTrace("Initializing the Razor workspace listener with pipe name {0}", pipeName);
            _razorWorkspaceListener = new RazorWorkspaceListener(_loggerFactory);
            _razorWorkspaceListener.EnsureInitialized(workspace, pipeName);

            projectsToInitialize = _projectIdWithDynamicFiles;
            // May as well clear out the collection, it will never get used again anyway.
            _projectIdWithDynamicFiles = [];
        }

        foreach (var projectId in projectsToInitialize)
        {
            _logger.LogTrace("{projectId} notifying a dynamic file for the first time", projectId);
            _razorWorkspaceListener.NotifyDynamicFile(projectId);
        }
    }

    public override void NotifyDynamicFile(ProjectId projectId)
    {
        _razorWorkspaceListener?.NotifyDynamicFile(projectId);
    }
}
