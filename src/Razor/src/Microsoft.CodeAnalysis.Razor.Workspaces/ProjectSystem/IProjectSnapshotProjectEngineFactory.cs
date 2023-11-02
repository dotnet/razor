// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal interface IProjectSnapshotProjectEngineFactory : IWorkspaceService
{
    RazorProjectEngine Create(
        RazorConfiguration configuration,
        RazorProjectFileSystem fileSystem,
        Action<RazorProjectEngineBuilder>? configure);

    IProjectEngineFactory? FindSerializableFactory(IProjectSnapshot project);
}
