// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;

namespace Microsoft.CodeAnalysis.Razor.Compiler.CSharp;

internal static class CompilationExtensions
{
    public static bool HasAddComponentParameter(this Compilation compilation)
    {
        return compilation.GetTypesByMetadataName("Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder")
            .Any(static t =>
                t.DeclaredAccessibility == Accessibility.Public &&
                t.GetMembers("AddComponentParameter")
                    .Any(static m => m.DeclaredAccessibility == Accessibility.Public));
    }

    public static bool HasCallableUtf8WriteLiteralOverload(this Compilation compilation, string probeTypeMetadataName)
    {
        var readOnlySpanType = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
        var byteType = compilation.GetSpecialType(SpecialType.System_Byte);
        if (readOnlySpanType is not INamedTypeSymbol readOnlySpanNamedType ||
            byteType.TypeKind == TypeKind.Error)
        {
            return false;
        }

        var readOnlySpanOfByte = readOnlySpanNamedType.Construct(byteType);
        var probeType = compilation.GetTypeByMetadataName(probeTypeMetadataName);
        if (probeType is null || probeType.TypeKind == TypeKind.Error)
        {
            return false;
        }

        for (var currentType = probeType; currentType is not null; currentType = currentType.BaseType)
        {
            foreach (var method in currentType.GetMembers("WriteLiteral").OfType<IMethodSymbol>())
            {
                if (method.IsStatic ||
                    !method.ReturnsVoid ||
                    method.Parameters.Length != 1 ||
                    !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, readOnlySpanOfByte))
                {
                    continue;
                }

                if (compilation.IsSymbolAccessibleWithin(method, probeType))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
