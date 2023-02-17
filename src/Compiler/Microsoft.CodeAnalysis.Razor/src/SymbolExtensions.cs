// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.CodeAnalysis.Razor;

internal static class SymbolExtensions
{
    internal static readonly SymbolDisplayFormat FullNameTypeDisplayFormat =
        SymbolDisplayFormat.FullyQualifiedFormat
            .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted)
            .RemoveMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    internal static bool HasFullName(this ISymbol symbol, string fullName)
    {
        // generally we expect this call to fail, so try and match on the simple name first, before
        // checking the fully qualified name, to prevent unnecesary string allocation from ToDisplayString 
        if (symbol.Name.Length > fullName.Length)
        {
            return false;
        }

        // the simple name doesn't include generic type params, so skip over them in the full name if they are present
        int fullNameEndIndex = fullName[^1] == '>' ? fullName.LastIndexOf('<') : fullName.Length;
        int fullNameStartIndex = fullName.LastIndexOf('.', fullNameEndIndex - 1) + 1;

        if (!fullName.AsSpan(fullNameStartIndex, fullNameEndIndex - fullNameStartIndex).Equals(symbol.Name.AsSpan(), StringComparison.Ordinal))
        {
            return false;
        }

        // The simple name matched, do a fully qualified comparison
        return symbol.ToDisplayString(FullNameTypeDisplayFormat).Equals(fullName, StringComparison.Ordinal);
    }
}
