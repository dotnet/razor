// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.Test.Common.LanguageServer;

internal class TestProjectSnapshotProjectEngineFactory : IProjectSnapshotProjectEngineFactory
{
    public Action<RazorProjectEngineBuilder>? Configure { get; set; }

    public RazorProjectEngine? Engine { get; set; }

    public RazorProjectEngine Create(
        RazorConfiguration configuration,
        RazorProjectFileSystem fileSystem,
        Action<RazorProjectEngineBuilder>? configure)
    {
        return Engine ?? RazorProjectEngine.Create(configuration, fileSystem, configure ?? Configure);
    }

    public IProjectEngineFactory? FindFactory(IProjectSnapshot project)
    {
        throw new NotImplementedException();
    }

    public IProjectEngineFactory? FindSerializableFactory(IProjectSnapshot project)
    {
        throw new NotImplementedException();
    }
}
