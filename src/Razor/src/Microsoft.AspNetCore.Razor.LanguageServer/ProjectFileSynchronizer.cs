// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class ProjectFileSynchronizer : IProjectFileChangeListener
    {
        private readonly SingleThreadedDispatcher _singleThreadedDispatcher;
        private readonly RazorProjectService _projectService;

        public ProjectFileSynchronizer(
            SingleThreadedDispatcher singleThreadedDispatcher,
            RazorProjectService projectService)
        {
            if (singleThreadedDispatcher is null)
            {
                throw new ArgumentNullException(nameof(singleThreadedDispatcher));
            }

            if (projectService is null)
            {
                throw new ArgumentNullException(nameof(projectService));
            }

            _singleThreadedDispatcher = singleThreadedDispatcher;
            _projectService = projectService;
        }

        public void ProjectFileChanged(string filePath, RazorFileChangeKind kind)
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            _singleThreadedDispatcher.AssertDispatcherThread();

            switch (kind)
            {
                case RazorFileChangeKind.Added:
                    _projectService.AddProject(filePath);
                    break;
                case RazorFileChangeKind.Removed:
                    _projectService.RemoveProject(filePath);
                    break;
            }
        }
    }
}
