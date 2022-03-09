// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.LiveShare.Razor
{
    public sealed class ProjectSnapshotHandleProxy
    {
        public ProjectSnapshotHandleProxy(
            Uri filePath!!,
            RazorConfiguration configuration!!,
            string rootNamespace,
            ProjectWorkspaceState projectWorkspaceState)
        {
            FilePath = filePath;
            Configuration = configuration;
            RootNamespace = rootNamespace;
            ProjectWorkspaceState = projectWorkspaceState;
        }

        public Uri FilePath { get; }

        public RazorConfiguration Configuration { get; }

        public string RootNamespace { get; }

        public ProjectWorkspaceState ProjectWorkspaceState { get; }

    }
}
