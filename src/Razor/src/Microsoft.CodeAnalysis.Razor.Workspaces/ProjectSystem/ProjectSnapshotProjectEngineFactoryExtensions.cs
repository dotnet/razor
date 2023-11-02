// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal static class ProjectSnapshotProjectEngineFactoryExtensions
{
    public static RazorProjectEngine Create(
        this IProjectSnapshotProjectEngineFactory factory,
        IProjectSnapshot project)
        => factory.Create(
            project.Configuration,
            RazorProjectFileSystem.Create(Path.GetDirectoryName(project.FilePath)),
            configure: null);

    public static RazorProjectEngine Create(
        this IProjectSnapshotProjectEngineFactory factory,
        IProjectSnapshot project,
        Action<RazorProjectEngineBuilder>? configure)
        => factory.Create(
            project.Configuration,
            RazorProjectFileSystem.Create(Path.GetDirectoryName(project.FilePath)),
            configure);

    public static RazorProjectEngine Create(
        this IProjectSnapshotProjectEngineFactory factory,
        IProjectSnapshot project, RazorProjectFileSystem fileSystem,
        Action<RazorProjectEngineBuilder>? configure)
        => factory.Create(project.Configuration, fileSystem, configure);

    public static RazorProjectEngine Create(
        this IProjectSnapshotProjectEngineFactory factory,
        RazorConfiguration configuration,
        string directoryPath,
        Action<RazorProjectEngineBuilder>? configure)
        => factory.Create(configuration, RazorProjectFileSystem.Create(directoryPath), configure);

}
