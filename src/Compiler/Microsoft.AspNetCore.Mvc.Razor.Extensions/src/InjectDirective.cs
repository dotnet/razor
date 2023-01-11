﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public static class InjectDirective
{
    public static readonly DirectiveDescriptor Directive = DirectiveDescriptor.CreateDirective(
        "inject",
        DirectiveKind.SingleLine,
        builder =>
        {
            builder
                .AddTypeToken(RazorExtensionsResources.InjectDirective_TypeToken_Name, RazorExtensionsResources.InjectDirective_TypeToken_Description)
                .AddMemberToken(RazorExtensionsResources.InjectDirective_MemberToken_Name, RazorExtensionsResources.InjectDirective_MemberToken_Description);

            builder.Usage = DirectiveUsage.FileScopedMultipleOccurring;
            builder.Description = RazorExtensionsResources.InjectDirective_Description;
        });

    public static RazorProjectEngineBuilder Register(RazorProjectEngineBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AddDirective(Directive);
        builder.Features.Add(new Pass());
        builder.AddTargetExtension(new InjectTargetExtension());
        return builder;
    }

    internal class Pass : IntermediateNodePassBase, IRazorDirectiveClassifierPass
    {
        // Runs after the @model and @namespace directives
        public override int Order => 10;

        protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
        {
            if (documentNode.DocumentKind != RazorPageDocumentClassifierPass.RazorPageDocumentKind &&
               documentNode.DocumentKind != MvcViewDocumentClassifierPass.MvcViewDocumentKind)
            {
                // Not a MVC file. Skip.
                return;
            }

            var visitor = new Visitor();
            visitor.Visit(documentNode);
            var modelType = ModelDirective.GetModelType(documentNode);

            var properties = new HashSet<string>(StringComparer.Ordinal);

            for (var i = visitor.Directives.Count - 1; i >= 0; i--)
            {
                var directive = visitor.Directives[i];
                var tokens = directive.Tokens.ToArray();
                if (tokens.Length < 2)
                {
                    continue;
                }

                var typeName = tokens[0].Content;
                var memberName = tokens[1].Content;

                if (!properties.Add(memberName))
                {
                    continue;
                }

                typeName = typeName.Replace("<TModel>", "<" + modelType + ">");

                var injectNode = new InjectIntermediateNode()
                {
                    TypeName = typeName,
                    MemberName = memberName,
                };

                visitor.Class.Children.Add(injectNode);
            }
        }
    }

    private class Visitor : IntermediateNodeWalker
    {
        public ClassDeclarationIntermediateNode Class { get; private set; }

        public IList<DirectiveIntermediateNode> Directives { get; } = new List<DirectiveIntermediateNode>();

        public override void VisitClassDeclaration(ClassDeclarationIntermediateNode node)
        {
            if (Class == null)
            {
                Class = node;
            }

            base.VisitClassDeclaration(node);
        }

        public override void VisitDirective(DirectiveIntermediateNode node)
        {
            if (node.Directive == Directive)
            {
                Directives.Add(node);
            }
        }
    }

    #region Obsolete
    [Obsolete("This method is obsolete and will be removed in a future version.")]
    public static IRazorEngineBuilder Register(IRazorEngineBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AddDirective(Directive);
        builder.Features.Add(new Pass());
        builder.AddTargetExtension(new InjectTargetExtension());
        return builder;
    }
    #endregion
}
