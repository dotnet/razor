﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language;

internal static partial class INamedTypeSymbolExtensions
{
    public static bool IsViewComponent(this INamedTypeSymbol symbol, INamedTypeSymbol viewComponentAttribute, INamedTypeSymbol? nonViewComponentAttribute)
        => SymbolCache.GetNamedTypeSymbolData(symbol).IsViewComponent(viewComponentAttribute, nonViewComponentAttribute);
}
