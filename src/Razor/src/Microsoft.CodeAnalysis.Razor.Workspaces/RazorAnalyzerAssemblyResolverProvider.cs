// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.NET.Sdk.Razor.SourceGenerators;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal static class RazorAnalyzerAssemblyResolverProvider
{
    internal static void EnsureRazorAssemblyLoaderHooked()
    {
        if (RazorAnalyzerAssemblyResolver.AssemblyResolver is null)
        {
            var compilerAssembly = typeof(RazorSourceGenerator).Assembly!;
            var compilerAssemblyName = compilerAssembly.GetName();
            RazorAnalyzerAssemblyResolver.AssemblyResolver = (a) => a.Name == compilerAssemblyName.Name ? compilerAssembly : null;
        }
    }
}
