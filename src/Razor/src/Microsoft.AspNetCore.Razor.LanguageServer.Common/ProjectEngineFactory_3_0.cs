// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Common;

internal class ProjectEngineFactory_3_0 : ProjectEngineFactory
{
    protected override string AssemblyName { get; } = "Microsoft.AspNetCore.Mvc.Razor.Extensions";

    protected override void PreInitialize(RazorProjectEngineBuilder builder) => CompilerFeatures.Register(builder);
}
