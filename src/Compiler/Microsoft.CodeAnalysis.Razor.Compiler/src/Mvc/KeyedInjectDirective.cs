// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public static class KeyedInjectDirective
{
    public static readonly DirectiveDescriptor Directive = DirectiveDescriptor.CreateDirective(
        "keyedinject",
        DirectiveKind.SingleLine,
        builder =>
        {
            builder
                .AddTypeToken(RazorExtensionsResources.KeyedInjectDirective_TypeToken_Name, RazorExtensionsResources.KeyedInjectDirective_TypeToken_Description)
                .AddMemberToken(RazorExtensionsResources.KeyedInjectDirective_MemberToken_Name, RazorExtensionsResources.KeyedInjectDirective_MemberToken_Description)
                .AddStringToken(RazorExtensionsResources.KeyedInjectDirective_KeyToken_Name, RazorExtensionsResources.KeyedInjectDirective_KeyToken_Description);

            builder.Usage = DirectiveUsage.FileScopedMultipleOccurring;
            builder.Description = RazorExtensionsResources.KeyedInjectDirective_Description;
        });

    public static RazorProjectEngineBuilder Register(RazorProjectEngineBuilder builder, bool considerNullabilityEnforcement)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AddDirective(Directive);
        builder.Features.Add(new Pass());
        builder.AddTargetExtension(new KeyedInjectTargetExtension(considerNullabilityEnforcement));
        return builder;
    }

    internal sealed class Pass : IntermediateNodePassBase, IRazorDirectiveClassifierPass
    {
        // Runs after the @model and @namespace directives
        public override int Order => 10;

        protected override void ExecuteCore(
            RazorCodeDocument codeDocument,
            DocumentIntermediateNode documentNode,
            CancellationToken cancellationToken)
        {
            if (documentNode.DocumentKind != RazorPageDocumentClassifierPass.RazorPageDocumentKind &&
               documentNode.DocumentKind != MvcViewDocumentClassifierPass.MvcViewDocumentKind)
            {
                // Not a MVC file. Skip.
                return;
            }

            var visitor = new Visitor();
            visitor.Visit(documentNode);
            var modelType = ModelDirective.GetModelType(documentNode).Content;

            // Stop collisions with existing inject directives
            var existingMembers = new HashSet<string>(StringComparer.Ordinal);
            if (visitor.Class != null)
            {
                foreach (var property in visitor.Class.Children
                    .OfType<InjectIntermediateNode>())
                {
                    if (!string.IsNullOrEmpty(property.MemberName))
                    {
                        existingMembers.Add(property.MemberName);
                    }
                }
            }

            var properties = new HashSet<string>(StringComparer.Ordinal);

            for (var i = visitor.Directives.Count - 1; i >= 0; i--)
            {
                var directive = visitor.Directives[i];
                var tokens = directive.Children.OfType<DirectiveTokenIntermediateNode>().ToArray();
                var isMalformed = directive is MalformedDirectiveIntermediateNode;

                var hasType = tokens.Length > 0 && !string.IsNullOrWhiteSpace(tokens[0].Content);
                Debug.Assert(hasType || isMalformed);
                var typeName = hasType ? tokens[0].Content : string.Empty;
                var typeSpan = hasType ? tokens[0].Source : directive.Source?.GetZeroWidthEndSpan();

                var hasMemberName = tokens.Length > 1 && !string.IsNullOrWhiteSpace(tokens[1].Content);
                Debug.Assert(hasMemberName || isMalformed);
                var memberName = hasMemberName ? tokens[1].Content : null;
                var memberSpan = hasMemberName ? tokens[1].Source : null;
                // continue if the membername is in any existing inject statement or in a previous keyedinject statement
                if (hasMemberName && (!properties.Add(memberName!) || existingMembers.Contains(memberName!)))
                {
                    continue;
                }

                var hasKeyName = tokens.Length > 2 && !string.IsNullOrWhiteSpace(tokens[2].Content);
                Debug.Assert(hasKeyName || isMalformed);
                var keyName = hasKeyName ? ValidateStringToken(tokens[2].Content) : null;
                var keySpan = hasKeyName ? tokens[2].Source : null;

                const string tModel = "<TModel>";
                if (typeName.EndsWith(tModel, StringComparison.Ordinal))
                {
                    typeName = typeName[..^tModel.Length] + "<" + modelType + ">";
                    if (typeSpan.HasValue)
                    {
                        typeSpan = new SourceSpan(typeSpan.Value.FilePath, typeSpan.Value.AbsoluteIndex, typeSpan.Value.LineIndex, typeSpan.Value.CharacterIndex, typeSpan.Value.Length - tModel.Length, typeSpan.Value.LineCount, typeSpan.Value.EndCharacterIndex - tModel.Length);
                    }
                }

                var injectNode = new KeyedInjectIntermediateNode()
                {
                    TypeName = typeName,
                    MemberName = memberName,
                    TypeSource = typeSpan,
                    MemberSource = memberSpan,
                    KeyName = keyName,
                    KeySource = keySpan,
                    IsMalformed = isMalformed
                };

                visitor.Class!.Children.Add(injectNode);
            }
        }

        private static string ValidateStringToken(string token)
        {
            // Tokens aren't captured if they're malformed. Therefore, this method will
            // always be called with a valid token content.
            Debug.Assert(token.StartsWith("\"", StringComparison.Ordinal));
            Debug.Assert(token.EndsWith("\"", StringComparison.Ordinal));

            return token;
        }
    }

    private class Visitor : IntermediateNodeWalker
    {
        public ClassDeclarationIntermediateNode? Class { get; private set; }

        public IList<IntermediateNode> Directives { get; } = [];

        public override void VisitClassDeclaration(ClassDeclarationIntermediateNode node)
        {
            Class ??= node;

            base.VisitClassDeclaration(node);
        }

        public override void VisitDirective(DirectiveIntermediateNode node)
        {
            if (node.Directive == Directive)
            {
                Directives.Add(node);
            }
        }

        public override void VisitMalformedDirective(MalformedDirectiveIntermediateNode node)
        {
            if (node.Directive == Directive)
            {
                Directives.Add(node);
            }
        }
    }
}
