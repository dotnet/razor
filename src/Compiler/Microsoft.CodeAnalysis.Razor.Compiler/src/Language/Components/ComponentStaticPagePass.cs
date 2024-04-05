// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components;

internal class ComponentStaticPagePass : ComponentIntermediateNodePassBase, IRazorDirectiveClassifierPass
{
    // This pass is only used to produce diagnostics if you use @staticpage incorrectly.
    // When used correctly, the behavior is implemented inside ComponentPageDirectivePass
    // as it's just a modification to the @page output.

    protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
    {
        if (codeDocument == null)
        {
            throw new ArgumentNullException(nameof(codeDocument));
        }

        if (documentNode == null)
        {
            throw new ArgumentNullException(nameof(documentNode));
        }

        var staticPageDirectives = documentNode.FindDirectiveReferences(ComponentStaticPageDirective.Directive);
        if (staticPageDirectives.Count == 0)
        {
            return;
        }

        var isPage = documentNode.FindDirectiveReferences(ComponentPageDirective.Directive).Any();
        if (!isPage)
        {
            for (var i = 0; i < staticPageDirectives.Count; i++)
            {
                var staticPageDirective = staticPageDirectives[i].Node;
                staticPageDirective.Diagnostics.Add(ComponentDiagnosticFactory.CreateStaticPageDirective_MustCombineWithPage(staticPageDirective.Source));
            }
        }

        var hasRenderMode = documentNode.FindDirectiveReferences(ComponentRenderModeDirective.Directive).Any();
        if (hasRenderMode)
        {
            for (var i = 0; i < staticPageDirectives.Count; i++)
            {
                var staticPageDirective = staticPageDirectives[i].Node;
                staticPageDirective.Diagnostics.Add(ComponentDiagnosticFactory.CreateStaticPageDirective_MustNotCombineWithRenderMode(staticPageDirective.Source));
            }
        }
    }
}
