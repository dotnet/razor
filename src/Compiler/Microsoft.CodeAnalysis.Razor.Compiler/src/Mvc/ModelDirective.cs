// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public static class ModelDirective
{
    public static readonly DirectiveDescriptor Directive = DirectiveDescriptor.CreateDirective(
        "model",
        DirectiveKind.SingleLine,
        builder =>
        {
            builder.AddTypeToken(RazorExtensionsResources.ModelDirective_TypeToken_Name, RazorExtensionsResources.ModelDirective_TypeToken_Description);
            builder.Usage = DirectiveUsage.FileScopedSinglyOccurring;
            builder.Description = RazorExtensionsResources.ModelDirective_Description;
        });

    public static RazorProjectEngineBuilder Register(RazorProjectEngineBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AddDirective(Directive);
        builder.Features.Add(new Pass());
        return builder;
    }

    public static string GetModelType(DocumentIntermediateNode document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var @class = document.FindPrimaryClass();
        return GetModelType(document, @class).Content;
    }

    private static IntermediateToken GetModelType(DocumentIntermediateNode document, ClassDeclarationIntermediateNode classNode)
    {
        var directives = document.FindDirectiveReferences(Directive, includeMalformed: true);
        if (directives is [IntermediateNodeReference { Node.Children: [DirectiveTokenIntermediateNode firstToken, ..] } , ..])
        {
            return IntermediateToken.CreateCSharpToken(firstToken.Content, firstToken.Source);
        }
        else if (document.DocumentKind == RazorPageDocumentClassifierPass.RazorPageDocumentKind)
        {
            return IntermediateToken.CreateCSharpToken(classNode.ClassName);
        }
        else
        {
            return IntermediateToken.CreateCSharpToken("dynamic");
        }
    }

    internal class Pass : IntermediateNodePassBase, IRazorDirectiveClassifierPass
    {
        // Runs after the @inherits directive
        public override int Order => 5;

        protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
        {
            if (documentNode.DocumentKind != RazorPageDocumentClassifierPass.RazorPageDocumentKind &&
               documentNode.DocumentKind != MvcViewDocumentClassifierPass.MvcViewDocumentKind)
            {
                // Not a MVC file. Skip.
                return;
            }

            var @class = documentNode.FindPrimaryClass();
            var modelType = GetModelType(documentNode, @class);

            if (documentNode.Options.DesignTime)
            {
                // Alias the TModel token to a known type.
                // This allows design time compilation to succeed for Razor files where the token isn't replaced.
                var typeName = $"global::{typeof(object).FullName}";
                var usingNode = new UsingDirectiveIntermediateNode()
                {
                    Content = $"TModel = {typeName}"
                };

                var @namespace = documentNode.FindPrimaryNamespace();
                @namespace?.Children.Insert(0, usingNode);
                modelType.Source = null;
            }

            if (@class?.BaseType is BaseTypeWithModel { ModelType: not null } existingBaseType)
            {
                existingBaseType.ModelType = modelType;
            }
        }
    }
}
