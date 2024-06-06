// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;

internal sealed class RazorProjectInfoListener(
    IRazorProjectService projectService,
    IRazorProjectInfoDriver publisher)
    : IRazorProjectInfoListener, IOnInitialized
{
    private readonly IRazorProjectService _projectService = projectService;
    private readonly IRazorProjectInfoDriver _publisher = publisher;

    public async Task OnInitializedAsync(ILspServices services, CancellationToken cancellationToken)
    {
        _publisher.AddListener(this);

        // Add all existing projects
        foreach (var projectInfo in _publisher.GetLatestProjectInfo())
        {
            await AddOrUpdateProjectAsync(projectInfo, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask RemovedAsync(ProjectKey projectKey, CancellationToken cancellationToken)
    {
        await _projectService
            .UpdateProjectAsync(
                projectKey,
                configuration: null,
                rootNamespace: null,
                displayName: "",
                ProjectWorkspaceState.Default,
                documents: [],
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask UpdatedAsync(RazorProjectInfo projectInfo, CancellationToken cancellationToken)
    {
        await AddOrUpdateProjectAsync(projectInfo, cancellationToken).ConfigureAwait(false);
    }

    private Task AddOrUpdateProjectAsync(RazorProjectInfo projectInfo, CancellationToken cancellationToken)
    {
        return _projectService.AddOrUpdateProjectAsync(
            projectInfo.ProjectKey,
            projectInfo.FilePath,
            projectInfo.Configuration,
            projectInfo.RootNamespace,
            projectInfo.DisplayName,
            projectInfo.ProjectWorkspaceState,
            projectInfo.Documents,
            cancellationToken);
    }
}
