// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal class TestProjectSnapshotProjectEngineFactory : IProjectSnapshotProjectEngineFactory
{
    public Action<RazorProjectEngineBuilder>? Configure { get; set; }

    public RazorProjectEngine? Engine { get; set; }

    public RazorProjectEngine Create(
        RazorConfiguration configuration,
        RazorProjectFileSystem fileSystem,
        Action<RazorProjectEngineBuilder>? configure)
        => Engine ?? RazorProjectEngine.Create(configuration, fileSystem, b =>
        {
            configure?.Invoke(b);
            Configure?.Invoke(b);
        });

    public IProjectEngineFactory? FindSerializableFactory(IProjectSnapshot project)
        => throw new NotImplementedException();
}
