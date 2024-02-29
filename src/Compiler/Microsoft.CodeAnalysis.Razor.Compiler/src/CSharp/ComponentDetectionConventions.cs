﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.CodeAnalysis.Razor;

internal static class ComponentDetectionConventions
{
    public static bool IsComponent(INamedTypeSymbol symbol, INamedTypeSymbol icomponentSymbol)
    {
        if (symbol is null)
        {
            throw new ArgumentNullException(nameof(symbol));
        }

        if (icomponentSymbol is null)
        {
            throw new ArgumentNullException(nameof(icomponentSymbol));
        }

        return
            symbol.DeclaredAccessibility == Accessibility.Public &&
            !symbol.IsAbstract &&
            symbol.AllInterfaces.Contains(icomponentSymbol);
    }

    public static bool IsComponent(INamedTypeSymbol typeSymbol, string icomponentSymbolName)
    {
        if (typeSymbol.DeclaredAccessibility != Accessibility.Public ||
            typeSymbol.IsAbstract)
        {
            return false;
        }

        foreach (var interfaceSymbol in typeSymbol.AllInterfaces)
        {
            if (interfaceSymbol.HasFullName(icomponentSymbolName))
            {
                return true;
            }
        }

        return false;
    }
}
