﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public class PagesPropertyInjectionPass : IntermediateNodePassBase, IRazorOptimizationPass
{
    protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
    {
        if (documentNode.DocumentKind != RazorPageDocumentClassifierPass.RazorPageDocumentKind)
        {
            return;
        }

        var modelType = ModelDirective.GetModelType(documentNode);
        var visitor = new Visitor();
        visitor.Visit(documentNode);

        var @class = visitor.Class;

        var viewDataType = $"global::Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<{modelType}>";
        var vddProperty = new CSharpCodeIntermediateNode();
        vddProperty.Children.Add(new IntermediateToken()
        {
            Kind = TokenKind.CSharp,
            Content = $"public {viewDataType} ViewData => ({viewDataType})PageContext?.ViewData;",
        });
        @class.Children.Add(vddProperty);

        var modelProperty = new CSharpCodeIntermediateNode();
        modelProperty.Children.Add(new IntermediateToken()
        {
            Kind = TokenKind.CSharp,
            Content = $"public {modelType} Model => ViewData.Model;",
        });
        @class.Children.Add(modelProperty);
    }

    private class Visitor : IntermediateNodeWalker
    {
        public ClassDeclarationIntermediateNode Class { get; private set; }

        public override void VisitClassDeclaration(ClassDeclarationIntermediateNode node)
        {
            if (Class == null)
            {
                Class = node;
            }

            base.VisitClassDeclaration(node);
        }
    }
}
