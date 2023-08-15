// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.Components;

internal class ComponentRenderModeLoweringPass : ComponentIntermediateNodePassBase, IRazorOptimizationPass
{
    // Run after component lowering pass
    public override int Order => 50;

    protected override void ExecuteCore(RazorCodeDocument codeDocument, DocumentIntermediateNode documentNode)
    {
        if (!IsComponentDocument(documentNode))
        {
            return;
        }

        var @namespace = documentNode.FindPrimaryNamespace();
        var @class = documentNode.FindPrimaryClass();
        if (@namespace == null || @class == null)
        {
            // Nothing to do, bail. We can't function without the standard structure.
            return;
        }

        var references = documentNode.FindDescendantReferences<TagHelperDirectiveAttributeIntermediateNode>();
        foreach (var reference in references)
        {
            if (reference is { Node: TagHelperDirectiveAttributeIntermediateNode node, Parent: ComponentIntermediateNode } && node.TagHelper.IsRenderModeTagHelper())
            {
                var expression = node.Children[0] switch
                {
                    CSharpExpressionIntermediateNode cSharpNode => cSharpNode.Children[0],
                    IntermediateNode token => token
                };

                reference.Replace(new RenderModeIntermediateNode(expression));
                break;
            }
        }
    }
}
