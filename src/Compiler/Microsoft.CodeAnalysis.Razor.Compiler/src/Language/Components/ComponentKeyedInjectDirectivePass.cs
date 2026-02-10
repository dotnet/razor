// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components;

internal sealed class ComponentKeyedInjectDirectivePass : IntermediateNodePassBase, IRazorDirectiveClassifierPass
{
    protected override void ExecuteCore(
        RazorCodeDocument codeDocument,
        DocumentIntermediateNode documentNode,
        CancellationToken cancellationToken)
    {
        var visitor = new Visitor();
        visitor.Visit(documentNode);

        // Stop collisions with existing inject directives
        var existingMembers = new HashSet<string>(StringComparer.Ordinal);
        if (documentNode != null)
        {
            foreach (var property in documentNode.Children
                .OfType<InjectIntermediateNode>())
            {
                if (!string.IsNullOrEmpty(property.MemberName))
                {
                    existingMembers.Add(property.MemberName);
                }
            }
        }

        var properties = new HashSet<string>(StringComparer.Ordinal);
        var classNode = documentNode.FindPrimaryClass();

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
            var keyName = hasKeyName ? tokens[2].Content : null;
            var keySpan = hasKeyName ? tokens[2].Source : null;

            classNode!.Children.Add(new ComponentKeyedInjectIntermediateNode(typeName, memberName, typeSpan, memberSpan, isMalformed, keyName, keySpan));
        }
    }

    private class Visitor : IntermediateNodeWalker
    {
        public IList<IntermediateNode> Directives { get; } = [];

        public override void VisitDirective(DirectiveIntermediateNode node)
        {
            if (node.Directive == ComponentKeyedInjectDirective.Directive)
            {
                Directives.Add(node);
            }
        }

        public override void VisitMalformedDirective(MalformedDirectiveIntermediateNode node)
        {
            if (node.Directive == ComponentKeyedInjectDirective.Directive)
            {
                Directives.Add(node);
            }
        }
    }
}
