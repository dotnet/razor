// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#if !NET472

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Exports;

internal sealed class AssemblyLoadContextWrapper
{
    private static readonly ConcurrentDictionary<AssemblyName, Assembly?> s_loadedSharedAssemblies = new(AssemblyNameComparer.Default);

    private AssemblyLoadContext? _assemblyLoadContext;
    private readonly ImmutableDictionary<string, Assembly> _loadedAssemblies;

    private AssemblyLoadContextWrapper(AssemblyLoadContext assemblyLoadContext, ImmutableDictionary<string, Assembly> loadedFiles)
    {
        _assemblyLoadContext = assemblyLoadContext;
        _loadedAssemblies = loadedFiles;
    }

    public static bool TryLoadExtension(string assemblyFilePath, string? sharedDependenciesPath, [NotNullWhen(true)] out Assembly? assembly)
    {
        var dir = Path.GetDirectoryName(assemblyFilePath);
        var fileName = Path.GetFileName(assemblyFilePath);
        var fileNameNoExt = Path.GetFileNameWithoutExtension(assemblyFilePath);

        var loadContext = TryCreate(fileNameNoExt, dir!, sharedDependenciesPath);
        if (loadContext != null)
        {
            assembly = loadContext.GetAssembly(fileName);
            return true;
        }

        assembly = null;
        return false;
    }

    public static AssemblyLoadContextWrapper? TryCreate(string name, string assembliesDirectoryPath, string? sharedDependenciesPath)
    {
        try
        {
            var loadContext = CreateLoadContext(name, sharedDependenciesPath);
            var directory = new DirectoryInfo(assembliesDirectoryPath);
            var builder = new Dictionary<string, Assembly>();
            foreach (var file in directory.GetFiles("*.dll"))
            {
                if (!file.Name.Contains("ProjectEngineHost"))
                {
                    builder.Add(file.Name, loadContext.LoadFromAssemblyPath(file.FullName));
                }
            }

            return new AssemblyLoadContextWrapper(loadContext, builder.ToImmutableDictionary());
        }
        catch
        {
            return null;
        }
    }

    private static AssemblyLoadContext CreateLoadContext(string name, string? sharedDependenciesPath)
    {
        var loadContext = new AssemblyLoadContext(name);

        if (sharedDependenciesPath != null)    
        {
            loadContext.Resolving += (_, assemblyName) =>
            {
                if (assemblyName.Name is null)
                {
                    return null;
                }

                if (s_loadedSharedAssemblies.TryGetValue(assemblyName, out var loadedAssembly))
                {
                    return loadedAssembly;
                }

                var candidatePath = assemblyName.CultureName is not null
                    ? Path.Combine(sharedDependenciesPath, assemblyName.CultureName, $"{assemblyName.Name}.dll")
                    : Path.Combine(sharedDependenciesPath, $"{assemblyName.Name}.dll");

                if (File.Exists(candidatePath))
                {
                    loadedAssembly = loadContext.LoadFromAssemblyPath(candidatePath);
                }

                s_loadedSharedAssemblies.TryAdd(assemblyName, loadedAssembly);

                return s_loadedSharedAssemblies[assemblyName];
            };
        }

        return loadContext;
    }

    public Assembly GetAssembly(string name) => _loadedAssemblies[name];

    public MethodInfo? TryGetMethodInfo(string assemblyName, string className, string methodName)
    {
        try
        {
            return GetMethodInfo(assemblyName, className, methodName);
        }
        catch
        {
            return null;
        }
    }

    public MethodInfo GetMethodInfo(string assemblyName, string className, string methodName)
    {
        var assembly = GetAssembly(assemblyName);
        var completionHelperType = assembly.GetType(className);
        if (completionHelperType == null)
        {
            throw new ArgumentException($"{assembly.FullName} assembly did not contain {className} class");
        }
        var createCompletionProviderMethodInto = completionHelperType?.GetMethod(methodName);
        if (createCompletionProviderMethodInto == null)
        {
            throw new ArgumentException($"{className} from {assembly.FullName} assembly did not contain {methodName} method");
        }
        return createCompletionProviderMethodInto;
    }

    public void Dispose()
    {
        _assemblyLoadContext?.Unload();
        _assemblyLoadContext = null;
    }

    private sealed class AssemblyNameComparer : IEqualityComparer<AssemblyName>
    {
        public static readonly AssemblyNameComparer Default = new();

        public bool Equals(AssemblyName? x, AssemblyName? y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x == null || y == null)
                return false;

            return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.CultureName, y.CultureName, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(AssemblyName obj)
        {
            var hashCodeCombiner = new HashCodeCombiner();
            hashCodeCombiner.Add(new int[] {
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.CultureName ?? string.Empty)
            });

            return hashCodeCombiner.CombinedHash;
        }
    }
}

#endif
