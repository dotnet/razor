// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost;

internal static class IProjectEngineFactoryExtensions
{
    public static RazorProjectEngine Create(
        this IProjectEngineFactory factory,
        IProjectSnapshot projectSnapshot,
        Action<RazorProjectEngineBuilder>? configure = null)
        => factory.Create(
            projectSnapshot.Configuration,
            RazorProjectFileSystem.Create(Path.GetDirectoryName(projectSnapshot.FilePath)),
            configure);

    [return: NotNullIfNotNull(nameof(fallbackFactory))]
    public static RazorProjectEngine? Create(
        this IProjectEngineFactoryProvider factoryProvider,
        IProjectSnapshot projectSnapshot,
        IProjectEngineFactory? fallbackFactory = null,
        Action<RazorProjectEngineBuilder>? configure = null)
        => factoryProvider.Create(
            projectSnapshot.Configuration,
            rootDirectoryPath: Path.GetDirectoryName(projectSnapshot.FilePath).AssumeNotNull(),
            fallbackFactory,
            configure);

    [return: NotNullIfNotNull(nameof(fallbackFactory))]
    public static RazorProjectEngine? Create(
        this IProjectEngineFactoryProvider factoryProvider,
        RazorConfiguration configuration,
        string rootDirectoryPath,
        IProjectEngineFactory? fallbackFactory = null,
        Action<RazorProjectEngineBuilder>? configure = null)
        => factoryProvider.GetFactory(configuration, fallbackFactory) is IProjectEngineFactory factory
            ? factory.Create(configuration, RazorProjectFileSystem.Create(rootDirectoryPath), configure)
            : null;
}
