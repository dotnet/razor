// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class RazorFileSynchronizer : IRazorFileChangeListener
{
    private readonly ProjectSnapshotManagerDispatcher _dispatcher;
    private readonly RazorProjectService _projectService;

    public RazorFileSynchronizer(ProjectSnapshotManagerDispatcher dispatcher, RazorProjectService projectService)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
    }

    public async ValueTask RazorFileChangedAsync(string filePath, RazorFileChangeKind kind, CancellationToken cancellationToken)
    {
        if (filePath is null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        await _dispatcher.SwitchToAsync(cancellationToken);

        switch (kind)
        {
            case RazorFileChangeKind.Added:
                _projectService.AddDocument(filePath);
                break;
            case RazorFileChangeKind.Removed:
                _projectService.RemoveDocument(filePath);
                break;
        }
    }
}
