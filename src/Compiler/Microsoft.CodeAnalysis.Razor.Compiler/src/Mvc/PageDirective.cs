// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public class PageDirective
{
    public static readonly DirectiveDescriptor Directive = DirectiveDescriptor.CreateDirective(
        "page",
        DirectiveKind.SingleLine,
        builder =>
        {
            builder
                .AddOptionalIdentifierOrExpressionOrString(
                    RazorExtensionsResources.PageDirective_RouteToken_Name,
                    RazorExtensionsResources.PageDirective_RouteToken_Description)
            ;
            builder.Usage = DirectiveUsage.FileScopedSinglyOccurring;
            builder.Description = RazorExtensionsResources.PageDirective_Description;
        });

    private PageDirective(
        string? routeTemplate, DirectiveTokenIntermediateNode? routeTemplateNode,
        LazyIntermediateToken? routeTemplateToken, IntermediateNode directiveNode, SourceSpan? source)
    {
        RouteTemplate = routeTemplate;
        RouteTemplateNode = routeTemplateNode;
        RouteTemplateToken = routeTemplateToken;
        DirectiveNode = directiveNode;
        Source = source;
    }

    public string? RouteTemplate { get; }

    public DirectiveTokenIntermediateNode? RouteTemplateNode { get; }

    public IntermediateToken? RouteTemplateToken { get; }

    public IntermediateNode DirectiveNode { get; }

    public SourceSpan? Source { get; }

    public static RazorProjectEngineBuilder Register(RazorProjectEngineBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AddDirective(Directive);
        return builder;
    }

    public static bool TryGetPageDirective(DocumentIntermediateNode documentNode, [NotNullWhen(true)] out PageDirective? pageDirective)
    {
        var visitor = new Visitor();
        for (var i = 0; i < documentNode.Children.Count; i++)
        {
            visitor.Visit(documentNode.Children[i]);
        }

        if (visitor.DirectiveTokens == null)
        {
            pageDirective = null;
            return false;
        }

        var tokens = visitor.DirectiveTokens.ToList();
        var children = visitor.Children?.ToList();
        DirectiveTokenIntermediateNode? routeTemplateNode = null;
        LazyIntermediateToken? routeTemplateLazyToken = null;

        if (tokens is [var firstToken, ..])
        {
            routeTemplateNode = firstToken;
        }

        if (routeTemplateNode is null && children is [LazyIntermediateToken firstChild, ..])
        {
            routeTemplateLazyToken = firstChild;
        }

        var content = routeTemplateNode?.Content ?? routeTemplateLazyToken?.Content;
        var source = routeTemplateNode?.Source ?? routeTemplateLazyToken?.Source;

        var routeTemplate = TryGetQuotedContent(content);
        var sourceSpan = source;

        Debug.Assert(visitor.DirectiveNode is not null);

        pageDirective = new PageDirective(routeTemplate, routeTemplateNode, routeTemplateLazyToken, visitor.DirectiveNode, sourceSpan);
        return true;
    }

    private static string? TryGetQuotedContent(string? content)
    {
        // Tokens aren't captured if they're malformed. Therefore, this method will
        // always be called with a valid token content. However, we could also
        // receive an expression that is not a string literal. We will therefore
        // only try to parse the simple string literal case, and otherwise let the
        // C# expression parser determine the constant value.
        if (content is ['\"', .. var literal, '\"'])
        {
            return literal;
        }

        return null;
    }

    private class Visitor : IntermediateNodeWalker
    {
        public IntermediateNode DirectiveNode { get; private set; } = null!;

        public IEnumerable<DirectiveTokenIntermediateNode>? DirectiveTokens { get; private set; }

        public IntermediateNodeCollection? Children { get; private set; }

        public override void VisitDirective(DirectiveIntermediateNode node)
        {
            if (node.Directive == Directive)
            {
                DirectiveNode = node;
                DirectiveTokens = node.Tokens;
                Children = node.Children;
            }
        }

        public override void VisitMalformedDirective(MalformedDirectiveIntermediateNode node)
        {
            if (DirectiveTokens == null && node.Directive == Directive)
            {
                DirectiveNode = node;
                DirectiveTokens = node.Tokens;
                Children = node.Children;
            }
        }
    }
}
