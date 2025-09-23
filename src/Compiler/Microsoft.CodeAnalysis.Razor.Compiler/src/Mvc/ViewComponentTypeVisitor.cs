﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

internal class ViewComponentTypeVisitor : SymbolVisitor
{
    private readonly INamedTypeSymbol _viewComponentAttribute;
    private readonly INamedTypeSymbol? _nonViewComponentAttribute;
    private readonly List<INamedTypeSymbol> _results;

    public ViewComponentTypeVisitor(
        INamedTypeSymbol viewComponentAttribute,
        INamedTypeSymbol? nonViewComponentAttribute,
        List<INamedTypeSymbol> results)
    {
        _viewComponentAttribute = viewComponentAttribute;
        _nonViewComponentAttribute = nonViewComponentAttribute;
        _results = results;
    }

    public override void VisitAssembly(IAssemblySymbol symbol)
    {
        Visit(symbol.GlobalNamespace);
    }

    public override void VisitNamedType(INamedTypeSymbol symbol)
    {
        if (IsViewComponent(symbol))
        {
            _results.Add(symbol);
        }

        if (symbol.DeclaredAccessibility != Accessibility.Public)
        {
            return;
        }

        foreach (var member in symbol.GetTypeMembers())
        {
            Visit(member);
        }
    }

    public override void VisitNamespace(INamespaceSymbol symbol)
    {
        foreach (var member in symbol.GetMembers())
        {
            Visit(member);
        }
    }

    internal bool IsViewComponent(INamedTypeSymbol symbol)
    {
        if (_viewComponentAttribute == null)
        {
            return false;
        }

        return symbol.IsViewComponent(_viewComponentAttribute, _nonViewComponentAttribute);
    }
}
