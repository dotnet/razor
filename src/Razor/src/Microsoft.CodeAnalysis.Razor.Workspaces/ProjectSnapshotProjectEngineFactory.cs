// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.IO;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor
{
    internal abstract class ProjectSnapshotProjectEngineFactory : IWorkspaceService
    {
        public abstract IProjectEngineFactory FindFactory(ProjectSnapshot project);

        public abstract IProjectEngineFactory FindSerializableFactory(ProjectSnapshot project);

        public RazorProjectEngine Create(ProjectSnapshot project)
        {
            return Create(project, RazorProjectFileSystem.Create(Path.GetDirectoryName(project.FilePath)), null);
        }

        public RazorProjectEngine Create(ProjectSnapshot project!!, RazorProjectFileSystem fileSystem!!)
        {
            return Create(project, fileSystem, null);
        }

        public RazorProjectEngine Create(ProjectSnapshot project!!, Action<RazorProjectEngineBuilder> configure)
        {
            return Create(project, RazorProjectFileSystem.Create(Path.GetDirectoryName(project.FilePath)), configure);
        }

        public RazorProjectEngine Create(ProjectSnapshot project!!, RazorProjectFileSystem fileSystem!!, Action<RazorProjectEngineBuilder> configure)
        {
            return Create(project.Configuration, fileSystem, configure);
        }

        public RazorProjectEngine Create(RazorConfiguration configuration!!, string directoryPath!!, Action<RazorProjectEngineBuilder> configure)
        {
            return Create(configuration, RazorProjectFileSystem.Create(directoryPath), configure);
        }

        public abstract RazorProjectEngine Create(RazorConfiguration configuration, RazorProjectFileSystem fileSystem, Action<RazorProjectEngineBuilder> configure);
    }
}
