// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;

namespace Microsoft.CodeAnalysis.Razor.Compiler.Language.Extensions;

internal static class INamedTypeSymbolExtensions
{
    private static readonly ConditionalWeakTable<INamedTypeSymbol, NamedTypeSymbolCache> s_instance = new();

    public static bool IsViewComponent(this INamedTypeSymbol symbol, INamedTypeSymbol viewComponentAttribute, INamedTypeSymbol? nonViewComponentAttribute)
    {
        var cache = s_instance.GetValue(symbol, static s => new NamedTypeSymbolCache(s));

        return cache.IsViewComponent(viewComponentAttribute, nonViewComponentAttribute);
    }

    private sealed class NamedTypeSymbolCache(INamedTypeSymbol symbol)
    {
        private IsViewComponentResult? _isViewComponentResult;

        public bool IsViewComponent(INamedTypeSymbol viewComponentAttribute, INamedTypeSymbol? nonViewComponentAttribute)
        {
            var isViewComponentResult = _isViewComponentResult;
            if (isViewComponentResult is null
                || !SymbolEqualityComparer.Default.Equals(isViewComponentResult.viewComponentAttribute, viewComponentAttribute)
                || !SymbolEqualityComparer.Default.Equals(isViewComponentResult.nonViewComponentAttribute, nonViewComponentAttribute))
            {
                bool isViewComponent;
                if (symbol.DeclaredAccessibility != Accessibility.Public ||
                    symbol.IsAbstract ||
                    symbol.IsGenericType ||
                    attributeIsDefined(symbol, nonViewComponentAttribute))
                {
                    isViewComponent = false;
                }
                else
                {
                    isViewComponent = symbol.Name.EndsWith(ViewComponentTypes.ViewComponentSuffix, StringComparison.Ordinal) ||
                        attributeIsDefined(symbol, viewComponentAttribute);
                }

                isViewComponentResult = new(isViewComponent, viewComponentAttribute, nonViewComponentAttribute);
                _isViewComponentResult = isViewComponentResult;
            }

            return isViewComponentResult.isViewComponent;

            static bool attributeIsDefined(INamedTypeSymbol? type, INamedTypeSymbol? queryAttribute)
            {
                if (type == null || queryAttribute == null)
                {
                    return false;
                }

                foreach (var attribute in type.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, queryAttribute))
                    {
                        return true;
                    }
                }

                return attributeIsDefined(type.BaseType, queryAttribute);
            }
        }

        private sealed record class IsViewComponentResult(bool isViewComponent, INamedTypeSymbol viewComponentAttribute, INamedTypeSymbol? nonViewComponentAttribute);
    }
}
