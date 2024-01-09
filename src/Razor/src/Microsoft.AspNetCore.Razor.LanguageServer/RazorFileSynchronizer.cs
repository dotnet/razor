// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class RazorFileSynchronizer : IRazorFileChangeListener
{
    private readonly IProjectSnapshotManagerDispatcher _dispatcher;
    private readonly RazorProjectService _projectService;

    public RazorFileSynchronizer(
        IProjectSnapshotManagerDispatcher dispatcher,
        RazorProjectService projectService)
    {
        if (dispatcher is null)
        {
            throw new ArgumentNullException(nameof(dispatcher));
        }

        if (projectService is null)
        {
            throw new ArgumentNullException(nameof(projectService));
        }

        _dispatcher = dispatcher;
        _projectService = projectService;
    }

    public void RazorFileChanged(string filePath, RazorFileChangeKind kind)
    {
        if (filePath is null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        _dispatcher.AssertRunningOnScheduler();

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
