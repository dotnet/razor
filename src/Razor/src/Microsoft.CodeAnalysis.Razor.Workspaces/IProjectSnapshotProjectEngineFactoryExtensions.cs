// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor;

internal static class IProjectSnapshotProjectEngineFactoryExtensions
{
    public static RazorProjectEngine Create(this IProjectSnapshotProjectEngineFactory factory, IProjectSnapshot project)
    {
        return factory.Create(project, RazorProjectFileSystem.Create(Path.GetDirectoryName(project.FilePath)), null);
    }

    public static RazorProjectEngine Create(this IProjectSnapshotProjectEngineFactory factory, IProjectSnapshot project, RazorProjectFileSystem fileSystem)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (fileSystem is null)
        {
            throw new ArgumentNullException(nameof(fileSystem));
        }

        return factory.Create(project, fileSystem, configure: null);
    }

    public static RazorProjectEngine Create(this IProjectSnapshotProjectEngineFactory factory, IProjectSnapshot project, Action<RazorProjectEngineBuilder>? configure)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        return factory.Create(project, RazorProjectFileSystem.Create(Path.GetDirectoryName(project.FilePath)), configure);
    }

    public static RazorProjectEngine Create(this IProjectSnapshotProjectEngineFactory factory, IProjectSnapshot project, RazorProjectFileSystem fileSystem, Action<RazorProjectEngineBuilder>? configure)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (fileSystem is null)
        {
            throw new ArgumentNullException(nameof(fileSystem));
        }

        return factory.Create(project.Configuration, fileSystem, configure);
    }

    public static RazorProjectEngine Create(this IProjectSnapshotProjectEngineFactory factory, RazorConfiguration configuration, string directoryPath, Action<RazorProjectEngineBuilder>? configure)
    {
        if (configuration is null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (directoryPath is null)
        {
            throw new ArgumentNullException(nameof(directoryPath));
        }

        return factory.Create(configuration, RazorProjectFileSystem.Create(directoryPath), configure);
    }
}
