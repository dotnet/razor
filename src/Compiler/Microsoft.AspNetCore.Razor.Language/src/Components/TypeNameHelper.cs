// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;

namespace Microsoft.AspNetCore.Razor.Language;

internal static class TypeNameHelper
{
    private const string GlobalPrefix = "global::";

    private static readonly ImmutableArray<string> PredefinedTypeNames = new[]
    {
        "bool",
        "int",
        "string",
        "float",
        "double",
        "decimal",
        "byte",
        "short",
        "long",
        "char",
        "object",
        "dynamic",
        "uint",
        "ushort",
        "ulong",
        "sbyte",
        "nint",
        "nuint",
    }.ToImmutableArray();

    internal static string GetGloballyQualifiedNameIfNeeded(string typeName)
    {
        if (typeName.Length == 0)
        {
            return typeName;
        }

        if (typeName.StartsWith(GlobalPrefix, StringComparison.Ordinal))
        {
            return typeName;
        }

        // Mitigation for https://github.com/dotnet/razor-compiler/issues/332. When we add a reference to Roslyn
        // at this layer, we can do this property by using ParseTypeName and then rewriting the tree. For now, we
        // just skip prefixing tuples.
        if (typeName[0] == '(')
        {
            return typeName;
        }

        // Fast path, if the length doesn't fall within that of the
        // builtin c# types, then we can add global without further checks.
        if (typeName.Length is < 3 or > 7)
        {
            return GlobalPrefix + typeName;
        }

        foreach (var predefinedTypeName in PredefinedTypeNames)
        {
            if (typeName.Equals(predefinedTypeName, StringComparison.Ordinal))
            {
                return typeName;
            }
        }

        return GlobalPrefix + typeName;
    }

    public static void WriteGloballyQualifiedName(CodeWriter codeWriter, string typeName)
    {
        if (typeName == null)
        {
            throw new ArgumentNullException(nameof(typeName));
        }

        WriteGloballyQualifiedName(codeWriter, typeName.AsMemory());
    }

    internal static void WriteGloballyQualifiedName(CodeWriter codeWriter, ReadOnlyMemory<char> typeName)
    {
        if (typeName.Length == 0)
        {
            return;
        }

        var typeNameSpan = typeName.Span;

        if (typeNameSpan.StartsWith(GlobalPrefix.AsSpan(), StringComparison.Ordinal))
        {
            codeWriter.Write(typeName);
            return;
        }

        // Mitigation for https://github.com/dotnet/razor-compiler/issues/332. When we add a reference to Roslyn
        // at this layer, we can do this property by using ParseTypeName and then rewriting the tree. For now, we
        // just skip prefixing tuples.
        if (typeNameSpan[0] == '(')
        {
            codeWriter.Write(typeName);
            return;
        }

        // Fast path, if the length doesn't fall within that of the
        // builtin c# types, then we can add global without further checks.
        if (typeNameSpan.Length < 3 || typeNameSpan.Length > 7)
        {
            codeWriter.Write(GlobalPrefix);
            codeWriter.Write(typeName);
            return;
        }

        foreach (var predefinedTypeName in PredefinedTypeNames)
        {
            if (typeNameSpan.Equals(predefinedTypeName.AsSpan(), StringComparison.Ordinal))
            {
                codeWriter.Write(typeName);
                return;
            }
        }

        codeWriter.Write(GlobalPrefix);
        codeWriter.Write(typeName);
    }
}
