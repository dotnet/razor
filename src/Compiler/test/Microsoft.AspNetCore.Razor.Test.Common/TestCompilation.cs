// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyModel;
using Xunit;

namespace Microsoft.CodeAnalysis;

public static class TestCompilation
{
    private static readonly ImmutableArray<string> _shimAliases = ImmutableArray.Create("shim");
    private static readonly ConcurrentDictionary<Assembly, IEnumerable<MetadataReference>> _referenceCache =
        new ConcurrentDictionary<Assembly, IEnumerable<MetadataReference>>();

    public static IEnumerable<MetadataReference> GetMetadataReferences(Assembly assembly, bool aliasShims = false)
    {
        var dependencyContext = DependencyContext.Load(assembly);

        var metadataReferences =
            from l in dependencyContext.CompileLibraries
            from p in ResolvePaths(l)
            let r = MetadataReference.CreateFromFile(p)
            select aliasShims && p.Contains("Shim.") ? r.WithAliases(_shimAliases) : r;

        return metadataReferences;
    }

    private static IEnumerable<string> ResolvePaths(CompilationLibrary library)
    {
#if NETFRAMEWORK
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            if (assemblies[i].GetName().Name == library.Name)
            {
                return new[] { assemblies[i].Location };
            }
        }
#endif

        try
        {
            return library.ResolveReferencePaths();
        }
        catch (InvalidOperationException)
        {
        }

        return Array.Empty<string>();
    }

    public static string AssemblyName => "TestAssembly";

    public static CSharpCompilation Create(Assembly assembly, SyntaxTree syntaxTree = null, bool aliasShims = false)
    {
        IEnumerable<SyntaxTree> syntaxTrees = null;

        if (syntaxTree != null)
        {
            syntaxTrees = new[] { syntaxTree };
        }

        if (!_referenceCache.TryGetValue(assembly, out IEnumerable<MetadataReference> metadataReferences))
        {
            metadataReferences = GetMetadataReferences(assembly, aliasShims: aliasShims);
            _referenceCache.TryAdd(assembly, metadataReferences);
        }

        var compilation = CSharpCompilation.Create(AssemblyName, syntaxTrees, metadataReferences, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            specificDiagnosticOptions: new KeyValuePair<string, ReportDiagnostic>[]
            {
                // Ignore warnings about conflicts due to referencing `Microsoft.AspNetCore.App` DLLs.
                // Won't be necessary after fixing https://github.com/dotnet/roslyn/issues/19640.
                new("CS1701", ReportDiagnostic.Suppress)
            }));

        EnsureValidCompilation(compilation);

        return compilation;
    }

    private static void EnsureValidCompilation(CSharpCompilation compilation)
    {
        using (var stream = new MemoryStream())
        {
            var emitResult = compilation.Emit(stream);
            var diagnostics = string.Join(
                Environment.NewLine,
                emitResult.Diagnostics.Select(d => CSharpDiagnosticFormatter.Instance.Format(d)));
            Assert.True(emitResult.Success, $"Compilation is invalid : {Environment.NewLine}{diagnostics}");
        }
    }
}
