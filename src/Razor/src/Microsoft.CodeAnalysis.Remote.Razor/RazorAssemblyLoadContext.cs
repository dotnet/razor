// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#if NET
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.CodeAnalysis.Remote.Razor;

internal sealed class RazorAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyLoadContext? _parent;
    private readonly string _baseDirectory;

    private Assembly? _razorCompilerAssembly;

    private object _loaderLock = new();

    public static readonly RazorAssemblyLoadContext Instance = new();

    public RazorAssemblyLoadContext()
        : base(isCollectible: true)
    {
        var thisAssembly = GetType().Assembly;
        _parent = GetLoadContext(thisAssembly);
        _baseDirectory = Path.GetDirectoryName(thisAssembly.Location) ?? "";
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var fileName = Path.Combine(_baseDirectory, assemblyName.Name + ".dll");
        if (File.Exists(fileName))
        {
            // when we are asked to load razor.compiler, we first have to see if Roslyn beat us to it.
            if (IsRazorCompiler(assemblyName))
            {
                // Take the loader lock before we even try and install the resolver.
                // This ensures that if we successfully install the resolver we can't resolve the assembly until it's actually loaded
                lock (_loaderLock)
                {
                    if (RazorAnalyzerAssemblyResolver.TrySetAssemblyResolver(ResolveAssembly, assemblyName))
                    {
                        // We were able to install the resolver. Load the assembly and keep a reference to it.
                        _razorCompilerAssembly = LoadFromAssemblyPath(fileName);
                        return _razorCompilerAssembly;
                    }
                    else
                    {
                        // Roslyn won the race, we need to find the compiler assembly it loaded.
                        while (true)
                        {
                            foreach (var alc in AssemblyLoadContext.All)
                            {
                                var roslynRazorCompiler = alc.Assemblies.SingleOrDefault(a => IsRazorCompiler(a.GetName()));
                                if (roslynRazorCompiler is not null)
                                {
                                    return roslynRazorCompiler;
                                }
                            }
                            // we didn't find it, so it's possible that the Roslyn loader is still in the process of loading it. Yield and try again.
                            Thread.Yield();
                        }
                    }
                }
            }

            return LoadFromAssemblyPath(fileName);
        }

        return _parent?.LoadFromAssemblyName(assemblyName);
    }

    private Assembly? ResolveAssembly(AssemblyName assemblyName)
    {
        if (IsRazorCompiler(assemblyName))
        {
            lock (_loaderLock)
            {
                Debug.Assert(_razorCompilerAssembly is not null);
                return _razorCompilerAssembly;
            }
        }

        return null;
    }

    private bool IsRazorCompiler(AssemblyName assemblyName) => assemblyName.Name?.Contains("Microsoft.CodeAnalysis.Razor.Compiler", StringComparison.OrdinalIgnoreCase) == true;
}
#endif
