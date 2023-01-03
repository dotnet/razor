// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common;

internal class ProjectEngineFactory_2_1 : ProjectEngineFactory
{
    protected override string AssemblyName { get; } = "Microsoft.AspNetCore.Mvc.Razor.Extensions.Version2_X";

    public override RazorProjectEngine Create(
        RazorConfiguration configuration,
        RazorProjectFileSystem fileSystem,
        Action<RazorProjectEngineBuilder> configure) => Create(configuration, fileSystem, configure, registerCompilerFeatures: false);
}
