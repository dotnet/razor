// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor;

internal interface IProjectSnapshotProjectEngineFactory : IWorkspaceService
{
    IProjectEngineFactory? FindFactory(IProjectSnapshot project);

    IProjectEngineFactory? FindSerializableFactory(IProjectSnapshot project);

    RazorProjectEngine Create(RazorConfiguration configuration, RazorProjectFileSystem fileSystem, Action<RazorProjectEngineBuilder>? configure);
}
